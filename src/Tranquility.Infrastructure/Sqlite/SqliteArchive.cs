using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Data.Sqlite;
using Tranquility.Application;
using Tranquility.Application.Abstractions;

namespace Tranquility.Infrastructure.Sqlite;

/// <summary>
/// SQLite-backed archive (ADR-0003): one WAL database per instance under the
/// configured data directory, one writer task per database (single-writer by
/// construction), open in-memory segments merged into every read for
/// freshness. Implements L2-ARC-001..004 storage.
/// </summary>
public sealed class SqliteArchive : IArchive, IAsyncDisposable
{
    private readonly TranquilityOptions _options;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, InstanceStore> _stores = new(StringComparer.Ordinal);

    public SqliteArchive(TranquilityOptions options)
    {
        _options = options;
    }

    private InstanceStore Store(string instance)
    {
        lock (_gate)
        {
            if (!_stores.TryGetValue(instance, out var store))
            {
                var directory = Path.Combine(
                    _options.DataDirectory ?? Path.Combine(AppContext.BaseDirectory, "data"), instance);
                Directory.CreateDirectory(directory);
                store = new InstanceStore(Path.Combine(directory, "archive.db"));
                _stores[instance] = store;
            }

            return store;
        }
    }

    public void RecordPacket(string instance, PacketRecord packet) =>
        Store(instance).Enqueue(new PacketItem(packet));

    public void RecordParameters(string instance, IReadOnlyList<ArchivedParameterValue> values) =>
        Store(instance).Enqueue(new ValuesItem(values));

    public Task FlushAsync(string instance, CancellationToken cancellationToken) =>
        Store(instance).FlushAsync(cancellationToken);

    public Task<IReadOnlyList<ArchivedParameterValue>> GetParameterHistoryAsync(
        string instance, string qualifiedName, long? startUs, long? stopUs,
        int limit, bool descending, CancellationToken cancellationToken) =>
        Store(instance).GetParameterHistoryAsync(qualifiedName, startUs, stopUs, limit, descending, cancellationToken);

    public async IAsyncEnumerable<IReadOnlyList<ArchivedParameterValue>> StreamParameterValuesAsync(
        string instance, IReadOnlyList<string> qualifiedNames, long? startUs, long? stopUs,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        const int BatchSize = 500;
        var merged = new List<ArchivedParameterValue>();
        foreach (var name in qualifiedNames)
        {
            merged.AddRange(await Store(instance).GetParameterHistoryAsync(
                name, startUs, stopUs, int.MaxValue, descending: false, cancellationToken));
        }

        foreach (var chunk in merged.OrderBy(v => v.GenTimeUs).Chunk(BatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return chunk;
        }
    }

    public Task<IReadOnlyList<PidInfo>> ListPidsAsync(string instance, CancellationToken cancellationToken) =>
        Task.FromResult(Store(instance).ListPids());

    public Task<IReadOnlyList<SegmentInfo>> ListSegmentsAsync(string instance, int pid, CancellationToken cancellationToken) =>
        Store(instance).ListSegmentsAsync(pid, cancellationToken);

    public IAsyncEnumerable<PacketRecord> ReadPacketsAsync(
        string instance, long? startUs, long? stopUs, CancellationToken cancellationToken) =>
        Store(instance).ReadPacketsAsync(startUs, stopUs, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        List<InstanceStore> stores;
        lock (_gate)
        {
            stores = _stores.Values.ToList();
            _stores.Clear();
        }

        foreach (var store in stores)
        {
            await store.DisposeAsync();
        }
    }

    private abstract record WorkItem;

    private sealed record PacketItem(PacketRecord Packet) : WorkItem;

    private sealed record ValuesItem(IReadOnlyList<ArchivedParameterValue> Values) : WorkItem;

    private sealed record FlushItem(TaskCompletionSource Done) : WorkItem;

    /// <summary>Per-instance store: one writer task, open segments in memory.</summary>
    private sealed class InstanceStore : IAsyncDisposable
    {
        private const int SegmentCloseCount = 500;
        private const long SegmentCloseSpanUs = 60_000_000;

        private readonly string _path;
        private readonly Channel<WorkItem> _work = Channel.CreateUnbounded<WorkItem>(
            new UnboundedChannelOptions { SingleReader = true });
        private readonly Task _writerTask;
        private readonly SqliteConnection _writeConnection;
        private readonly Lock _memGate = new();
        private readonly Dictionary<string, int> _pids = new(StringComparer.Ordinal);
        private readonly Dictionary<int, OpenSegment> _open = new();

        public InstanceStore(string path)
        {
            _path = path;
            _writeConnection = Open();
            InitializeSchema();
            LoadPids();
            _writerTask = WriteLoopAsync();
        }

        private SqliteConnection Open()
        {
            var connection = new SqliteConnection($"Data Source={_path}");
            connection.Open();
            using var pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
            pragma.ExecuteNonQuery();
            return connection;
        }

        private void InitializeSchema()
        {
            using var command = _writeConnection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS tm_packet(
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    gentime INTEGER NOT NULL, rectime INTEGER NOT NULL,
                    apid INTEGER NOT NULL, seqcount INTEGER NOT NULL,
                    link TEXT NOT NULL, data BLOB NOT NULL);
                CREATE INDEX IF NOT EXISTS ix_tm_packet_gentime ON tm_packet(gentime, id);
                CREATE TABLE IF NOT EXISTS pid_registry(
                    pid INTEGER PRIMARY KEY AUTOINCREMENT, fqn TEXT NOT NULL UNIQUE);
                CREATE TABLE IF NOT EXISTS pval_segment(
                    id INTEGER PRIMARY KEY AUTOINCREMENT, pid INTEGER NOT NULL,
                    seg_start INTEGER NOT NULL, seg_end INTEGER NOT NULL, count INTEGER NOT NULL);
                CREATE INDEX IF NOT EXISTS ix_pval_segment_pid ON pval_segment(pid, seg_start);
                CREATE TABLE IF NOT EXISTS pval(
                    segment_id INTEGER NOT NULL, pid INTEGER NOT NULL,
                    gentime INTEGER NOT NULL, acqtime INTEGER NOT NULL,
                    raw_type INTEGER NOT NULL, raw TEXT NOT NULL,
                    eng_type INTEGER NOT NULL, eng TEXT NOT NULL,
                    monitoring TEXT NOT NULL);
                CREATE INDEX IF NOT EXISTS ix_pval_pid_gentime ON pval(pid, gentime);
                """;
            command.ExecuteNonQuery();
        }

        private void LoadPids()
        {
            using var command = _writeConnection.CreateCommand();
            command.CommandText = "SELECT pid, fqn FROM pid_registry";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                _pids[reader.GetString(1)] = reader.GetInt32(0);
            }
        }

        public void Enqueue(WorkItem item) => _work.Writer.TryWrite(item);

        public async Task FlushAsync(CancellationToken cancellationToken)
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _work.Writer.TryWrite(new FlushItem(done));
            await done.Task.WaitAsync(cancellationToken);
        }

        private async Task WriteLoopAsync()
        {
            await foreach (var item in _work.Reader.ReadAllAsync())
            {
                try
                {
                    switch (item)
                    {
                        case PacketItem p:
                            PersistPacket(p.Packet);
                            break;
                        case ValuesItem v:
                            Accumulate(v.Values);
                            break;
                        case FlushItem f:
                            f.Done.TrySetResult();
                            break;
                    }
                }
                catch (Exception e) when (item is FlushItem f2)
                {
                    f2.Done.TrySetException(e);
                }
                catch (SqliteException)
                {
                    // A failed write must never kill the archive loop.
                }
            }
        }

        private void PersistPacket(PacketRecord packet)
        {
            using var command = _writeConnection.CreateCommand();
            command.CommandText = """
                INSERT INTO tm_packet(gentime, rectime, apid, seqcount, link, data)
                VALUES ($g, $r, $a, $s, $l, $d)
                """;
            command.Parameters.AddWithValue("$g", packet.GenTimeUs);
            command.Parameters.AddWithValue("$r", packet.RecTimeUs);
            command.Parameters.AddWithValue("$a", packet.Apid);
            command.Parameters.AddWithValue("$s", packet.SequenceCount);
            command.Parameters.AddWithValue("$l", packet.Link);
            command.Parameters.AddWithValue("$d", packet.Data);
            command.ExecuteNonQuery();
        }

        private void Accumulate(IReadOnlyList<ArchivedParameterValue> values)
        {
            List<(int Pid, OpenSegment Segment)>? toClose = null;
            lock (_memGate)
            {
                foreach (var value in values)
                {
                    var pid = GetOrCreatePid(value.QualifiedName);
                    if (!_open.TryGetValue(pid, out var segment))
                    {
                        _open[pid] = segment = new OpenSegment(value.GenTimeUs);
                    }

                    segment.Values.Add(value);
                    if (segment.Values.Count >= SegmentCloseCount ||
                        value.GenTimeUs - segment.StartUs >= SegmentCloseSpanUs)
                    {
                        (toClose ??= []).Add((pid, segment));
                        _open.Remove(pid);
                    }
                }
            }

            if (toClose is not null)
            {
                foreach (var (pid, segment) in toClose)
                {
                    PersistSegment(pid, segment);
                }
            }
        }

        private int GetOrCreatePid(string qualifiedName)
        {
            if (_pids.TryGetValue(qualifiedName, out var pid))
            {
                return pid;
            }

            using var command = _writeConnection.CreateCommand();
            command.CommandText = "INSERT INTO pid_registry(fqn) VALUES ($f); SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("$f", qualifiedName);
            pid = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            _pids[qualifiedName] = pid;
            return pid;
        }

        private void PersistSegment(int pid, OpenSegment segment)
        {
            using var transaction = _writeConnection.BeginTransaction();
            long segmentId;
            using (var command = _writeConnection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO pval_segment(pid, seg_start, seg_end, count) VALUES ($p, $s, $e, $c);
                    SELECT last_insert_rowid();
                    """;
                command.Parameters.AddWithValue("$p", pid);
                command.Parameters.AddWithValue("$s", segment.StartUs);
                command.Parameters.AddWithValue("$e", segment.Values[^1].GenTimeUs);
                command.Parameters.AddWithValue("$c", segment.Values.Count);
                segmentId = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }

            using (var command = _writeConnection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO pval(segment_id, pid, gentime, acqtime, raw_type, raw, eng_type, eng, monitoring)
                    VALUES ($sid, $p, $g, $a, $rt, $r, $et, $e, $m)
                    """;
                var sid = command.Parameters.AddWithValue("$sid", segmentId);
                var p = command.Parameters.AddWithValue("$p", pid);
                var g = command.Parameters.Add("$g", SqliteType.Integer);
                var a = command.Parameters.Add("$a", SqliteType.Integer);
                var rt = command.Parameters.Add("$rt", SqliteType.Integer);
                var r = command.Parameters.Add("$r", SqliteType.Text);
                var et = command.Parameters.Add("$et", SqliteType.Integer);
                var e = command.Parameters.Add("$e", SqliteType.Text);
                var m = command.Parameters.Add("$m", SqliteType.Text);
                foreach (var value in segment.Values)
                {
                    g.Value = value.GenTimeUs;
                    a.Value = value.AcqTimeUs;
                    (rt.Value, r.Value) = EncodeValue(value.RawValue);
                    (et.Value, e.Value) = EncodeValue(value.EngValue);
                    m.Value = value.Monitoring;
                    command.ExecuteNonQuery();
                }
            }

            transaction.Commit();
        }

        public Task<IReadOnlyList<ArchivedParameterValue>> GetParameterHistoryAsync(
            string qualifiedName, long? startUs, long? stopUs, int limit, bool descending,
            CancellationToken cancellationToken)
        {
            int pid;
            List<ArchivedParameterValue> merged = [];
            lock (_memGate)
            {
                if (!_pids.TryGetValue(qualifiedName, out pid))
                {
                    return Task.FromResult<IReadOnlyList<ArchivedParameterValue>>([]);
                }

                if (_open.TryGetValue(pid, out var open))
                {
                    merged.AddRange(open.Values);
                }
            }

            using (var connection = Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    SELECT gentime, acqtime, raw_type, raw, eng_type, eng, monitoring
                    FROM pval WHERE pid = $p
                    """;
                command.Parameters.AddWithValue("$p", pid);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    merged.Add(new ArchivedParameterValue(
                        qualifiedName,
                        DecodeValue(reader.GetInt32(2), reader.GetString(3)),
                        DecodeValue(reader.GetInt32(4), reader.GetString(5)),
                        reader.GetInt64(0), reader.GetInt64(1), reader.GetString(6)));
                }
            }

            var query = merged
                .Where(v => (startUs is not { } s || v.GenTimeUs >= s) && (stopUs is not { } e || v.GenTimeUs < e));
            query = descending ? query.OrderByDescending(v => v.GenTimeUs) : query.OrderBy(v => v.GenTimeUs);
            return Task.FromResult<IReadOnlyList<ArchivedParameterValue>>(query.Take(limit).ToList());
        }

        public IReadOnlyList<PidInfo> ListPids()
        {
            lock (_memGate)
            {
                return _pids.Select(kv => new PidInfo(kv.Value, kv.Key))
                    .OrderBy(p => p.Pid).ToList();
            }
        }

        public Task<IReadOnlyList<SegmentInfo>> ListSegmentsAsync(int pid, CancellationToken cancellationToken)
        {
            bool known;
            SegmentInfo? openInfo = null;
            lock (_memGate)
            {
                known = _pids.ContainsValue(pid);
                if (_open.TryGetValue(pid, out var open) && open.Values.Count > 0)
                {
                    openInfo = new SegmentInfo(open.StartUs, open.Values[^1].GenTimeUs, open.Values.Count);
                }
            }

            if (!known)
            {
                throw new NotFoundServiceException($"Parameter id {pid} not found in the archive");
            }

            var segments = new List<SegmentInfo>();
            using (var connection = Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT seg_start, seg_end, count FROM pval_segment WHERE pid = $p ORDER BY seg_start";
                command.Parameters.AddWithValue("$p", pid);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    segments.Add(new SegmentInfo(reader.GetInt64(0), reader.GetInt64(1), reader.GetInt32(2)));
                }
            }

            if (openInfo is not null)
            {
                segments.Add(openInfo);
            }

            return Task.FromResult<IReadOnlyList<SegmentInfo>>(segments);
        }

        public async IAsyncEnumerable<PacketRecord> ReadPacketsAsync(
            long? startUs, long? stopUs, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await FlushAsync(cancellationToken);
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT gentime, rectime, apid, seqcount, link, data FROM tm_packet
                WHERE ($s IS NULL OR gentime >= $s) AND ($e IS NULL OR gentime < $e)
                ORDER BY gentime, id
                """;
            command.Parameters.AddWithValue("$s", (object?)startUs ?? DBNull.Value);
            command.Parameters.AddWithValue("$e", (object?)stopUs ?? DBNull.Value);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new PacketRecord(
                    reader.GetInt64(0), reader.GetInt64(1), reader.GetInt32(2), reader.GetInt32(3),
                    reader.GetString(4), (byte[])reader.GetValue(5));
            }
        }

        private static (int Type, string Text) EncodeValue(object value) => value switch
        {
            ulong u => (0, u.ToString(CultureInfo.InvariantCulture)),
            long l => (1, l.ToString(CultureInfo.InvariantCulture)),
            double d => (2, d.ToString("R", CultureInfo.InvariantCulture)),
            string s => (3, s),
            bool b => (4, b ? "1" : "0"),
            _ => (3, value.ToString() ?? string.Empty),
        };

        private static object DecodeValue(int type, string text) => type switch
        {
            0 => ulong.Parse(text, CultureInfo.InvariantCulture),
            1 => long.Parse(text, CultureInfo.InvariantCulture),
            2 => double.Parse(text, CultureInfo.InvariantCulture),
            4 => text == "1",
            _ => text,
        };

        public async ValueTask DisposeAsync()
        {
            _work.Writer.TryComplete();
            await _writerTask;
            _writeConnection.Dispose();
        }

        private sealed class OpenSegment(long startUs)
        {
            public long StartUs { get; } = startUs;

            public List<ArchivedParameterValue> Values { get; } = [];
        }
    }
}
