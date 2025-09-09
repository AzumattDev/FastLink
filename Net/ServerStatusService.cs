// FastLink/Net/ServerStatusService.cs
// Replicates Valheim's server "is online?" + player counts using Steam's PingServer,
// and converts the result into a real ServerListEntryData (same flags Valheim shows).

using System;
using System.Collections.Generic;
using System.Linq;
using Steamworks;
using Splatform; // for GameVersion, PlatformManager etc.

namespace FastLink.Net;

internal static class ServerStatusService
{
    // How long a status is considered fresh before we re-ping.
    private static readonly TimeSpan TTL = TimeSpan.FromSeconds(10);

    // Cache: address:port -> last known status
    private static readonly Dictionary<string, Cached> _cache = new();

    // Ping queue (we keep 1 active so we don't spam)
    private static readonly Queue<WorkItem> _queue = new();
    private static WorkItem? _active;
    private static HServerQuery _activeQuery = HServerQuery.Invalid;

    // Steam callback handler for PingServer
    private static readonly ISteamMatchmakingPingResponse _pingHandler = new ISteamMatchmakingPingResponse(OnPingRespond, OnPingFailed);

    // For UI callbacks
    internal delegate void RowApplyCallback(int index, Definition def, ServerListEntryData entry);

    private static RowApplyCallback _apply;

    private struct Cached
    {
        public ServerListEntryData Entry;
        public DateTime WhenUtc;
    }

    private sealed class WorkItem
    {
        public readonly Definition Def;
        public readonly int Index;
        public readonly uint[] IpCandidates;
        public readonly ushort[] PortCandidates;

        // attempt = linear index into cartesian product (ip x port)
        public int Attempt;

        public WorkItem(Definition def, int index, uint[] ips, ushort[] ports)
        {
            Def = def;
            Index = index;
            IpCandidates = ips;
            PortCandidates = ports;
            Attempt = 0;
        }

        public bool TryCurrent(out uint ip, out ushort qport, out int ipIdx, out int portIdx)
        {
            ipIdx = Attempt % IpCandidates.Length;
            portIdx = Attempt / IpCandidates.Length;
            if (IpCandidates.Length == 0 || portIdx >= PortCandidates.Length)
            {
                ip = 0;
                qport = 0;
                return false;
            }

            ip = IpCandidates[ipIdx];
            qport = PortCandidates[portIdx];
            return true;
        }

        public bool Advance()
        {
            Attempt++;
            int total = IpCandidates.Length * PortCandidates.Length;
            return Attempt < total;
        }

        public string CacheKey => $"{Def.address}:{Def.port}";
    }


    // Public API ----------------------------------------------------------------

    /// <summary>
    /// Request fresh status for a single UI row (Definition at index). If we have a fresh cache entry,
    /// applies immediately; otherwise enqueues a Steam ping.
    /// </summary>
    internal static void RequestStatus(Definition def, int index, RowApplyCallback apply)
    {
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));

        var key = $"{def.address}:{def.port}";
        if (_cache.TryGetValue(key, out var cached) && (DateTime.UtcNow - cached.WhenUtc) < TTL)
        {
            _apply(index, def, cached.Entry);
            return;
        }

        if (!ResolveIPv4Both(def.address, out var ipHost, out var ipNet))
        {
            StoreAndApply(index, def, BuildUnavailable(def));
            return;
        }

        // Build query-port candidates (dedup’d, best-guess order)
        var ports = new List<ushort>
        {
            (ushort)(((int)def.port + 1) & 0xFFFF), // Valheim default
            27016, 27015, // very common host configs
            def.port // some providers reuse game port
        };
        var portCandidates = ports.Distinct().ToArray();

        // Try both IP byte orders
        var ips = (ipHost != ipNet) ? new[] { ipHost, ipNet } : new[] { ipHost };

        _queue.Enqueue(new WorkItem(def, index, ips, portCandidates));
        Pump();
    }

    /// <summary>
    /// Force refresh all definitions (used right after you rebuild the list).
    /// </summary>
    internal static void RefreshAll(IReadOnlyList<Definition> defs, RowApplyCallback apply)
    {
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        while (_queue.Count > 0) _queue.Dequeue();
        TryCancelActive();

        foreach (var def in defs)
            RequestStatus(def, index: -1, apply); // -1 means "apply by matching Definition"
    }

    // Internals -----------------------------------------------------------------

    private static void Pump()
    {
        if (_active != null || _queue.Count == 0) return;
        _active = _queue.Dequeue();

        if (!_active.TryCurrent(out var ip, out var qport, out var ipIdx, out var portIdx))
        {
            // no candidates at all
            FailActiveAndFinish();
            return;
        }

        FastLink.FastLinkPlugin.FastLinkLogger.LogDebug($"[Status] Pinging {_active.Def.address}:{qport} (game port {_active.Def.port}) [ip#{ipIdx},port#{portIdx}]");

        _activeQuery = SteamMatchmakingServers.PingServer(ip, qport, _pingHandler);
    }

    private static void OnPingRespond(gameserveritem_t data)
    {
        FastLink.FastLinkPlugin.FastLinkLogger.LogDebug($"[Status] Responded {data.GetServerName()} players {data.m_nPlayers}/{data.m_nMaxPlayers} {data.m_bPassword} {data.GetGameDescription()} {data.GetGameDir()}");

        if (_active == null) return;

        var entry = BuildEntryFromPing(data, _active.Def);
        Store(entry, _active.CacheKey);

        // If caller passed -1, we still notify; the callback can map Definition->row.
        _apply?.Invoke(_active.Index, _active.Def, entry);

        FinishPing();
    }

    private static void OnPingFailed()
    {
        FastLink.FastLinkPlugin.FastLinkLogger.LogDebug("[Status] Ping FAILED");

        if (_active == null) return;

        // Try next candidate for this same item
        if (_active.Advance())
        {
            // re-queue the same work item (preserve order: try all candidates before moving on)
            var retry = _active;
            _active = null;
            _activeQuery = HServerQuery.Invalid;
            _queue.Enqueue(retry);
            Pump();
            return;
        }

        // Exhausted candidates
        FailActiveAndFinish();
    }

    private static void FailActiveAndFinish()
    {
        var entry = BuildUnavailable(_active.Def);
        Store(entry, _active.CacheKey);
        _apply?.Invoke(_active.Index, _active.Def, entry);
        FinishPing();
    }

    private static void FinishPing()
    {
        _active = null;
        _activeQuery = HServerQuery.Invalid;
        if (_queue.Count > 0) Pump();
    }

    private static void TryCancelActive()
    {
        if (_activeQuery != HServerQuery.Invalid)
            SteamMatchmakingServers.CancelServerQuery(_activeQuery);
        _activeQuery = HServerQuery.Invalid;
        _active = null;
    }

    private static void StoreAndApply(int index, Definition def, ServerListEntryData entry)
    {
        Store(entry, $"{def.address}:{def.port}");
        _apply?.Invoke(index, def, entry); // index may be -1; caller will handle that case
    }

    private static void Store(ServerListEntryData entry, string key)
    {
        _cache[key] = new Cached { Entry = entry, WhenUtc = DateTime.UtcNow };
    }

    // Builders ------------------------------------------------------------------

    private static ServerListEntryData BuildUnavailable(Definition def)
    {
        var md = new ServerMatchmakingData(DateTime.UtcNow); // NotAvailable
        var sd = new ServerData(BuildJoinData(def), md);
        return new ServerListEntryData(sd, def.serverName);
    }

    private static ServerListEntryData BuildEntryFromPing(gameserveritem_t i, Definition def)
    {
        GameVersion gv;
        uint netVer;
        string[] mods;
        ZSteamMatchmaking.DecodeTags(i.GetGameTags(), out gv, out netVer, out mods);

        var addr = new SteamNetworkingIPAddr();
        addr.SetIPv4(i.m_NetAdr.GetIP(), i.m_NetAdr.GetConnectionPort());

        var md = new ServerMatchmakingData(
            DateTime.UtcNow,
            i.GetServerName(),
            (uint)i.m_nPlayers,
            (uint)i.m_nMaxPlayers,
            PlatformUserID.None,
            gv,
            netVer,
            null,
            i.m_bPassword,
            //PlatformManager.DistributionPlatform.Platform,
            new Platform("Steam"),
            mods);

        var sd = new ServerData(new ServerJoinData(new ServerJoinDataDedicated(addr.GetIPv4(), addr.m_port)), md);
        return new ServerListEntryData(sd, def.serverName);
    }

    private static ServerJoinData BuildJoinData(Definition def)
    {
        // best-effort join data for tooltip display when offline
        if (!ResolveIPv4Both(def.address, out var ipHost, out var ipNet)) ipHost = 0;
        var addr = new SteamNetworkingIPAddr();
        addr.SetIPv4(ipHost, def.port);
        return new ServerJoinData(new ServerJoinDataDedicated(addr.GetIPv4(), addr.m_port));
    }

    // Utilities -----------------------------------------------------------------

    /*private static uint ResolveIPv4(string host)
    {
        try
        {
            // Prefer Valheim's resolver; it returns an IPv4 first when possible
            var ip = ZSteamMatchmaking.instance?.FindIP(host);
            if (ip == null) return 0;
            if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return 0;

            var b = ip.GetAddressBytes();  // [A,B,C,D]
            // Host-order little endian: A + (B<<8) + (C<<16) + (D<<24)
            return (uint)(b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24));
        }
        catch (Exception e)
        {
            FastLink.FastLinkPlugin.FastLinkLogger.LogDebug($"ResolveIPv4 failed for {host}: {e}");
            return 0;
        }
    }*/
    private static bool ResolveIPv4Both(string host, out uint hostOrder, out uint networkOrder)
    {
        hostOrder = 0;
        networkOrder = 0;
        try
        {
            System.Net.IPAddress ip = null;

            if (ZSteamMatchmaking.m_instance != null)
            {
                var v = ZSteamMatchmaking.instance.FindIP(host);
                if (v != null && v.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    ip = v;
            }

            if (ip == null)
            {
                var he = System.Net.Dns.GetHostEntry(host);
                foreach (var a in he.AddressList)
                    if (a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        ip = a;
                        break;
                    }
            }

            if (ip == null) return false;

            var b = ip.GetAddressBytes(); // A.B.C.D
            hostOrder = (uint)(b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24));
            networkOrder = (uint)System.Net.IPAddress.HostToNetworkOrder(BitConverter.ToInt32(b, 0));
            return true;
        }
        catch (Exception e)
        {
            FastLink.FastLinkPlugin.FastLinkLogger.LogDebug($"ResolveIPv4Both failed for {host}: {e}");
            return false;
        }
    }
}