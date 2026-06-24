using System.Collections.Generic;

namespace XBVault.Models;

public static class MemeLines
{
    // Dial-up / modem nostalgia
    public static readonly string[] DialUp =
    {
        "ATDT 555-2368",
        "Negotiating baud rate...",
        "Baud rate: 9600",
        "Baud rate: 14400",
        "Baud rate: 28800",
        "Baud rate: 33600",
        "Baud rate: 56000",
        "Winsock not found!",
        "TCP/IP stack error. Retry? (Y/N)",
        "PPP negotiation failed. Retrying...",
        "You've got mail!",
        "No new messages.",
        "Check error 47 on line 11",
        "Packet collision detected. Retransmitting...",
        "Connection interrupted. Reconnecting...",
        "Bad CRC. Resending packet 0x4A...",
        "Downloading: 1 of 273 items...",
        "Protocol negotiation: V.90",
        "Handshake error: carrier lost",
        "Remote modem not responding",
        "Connection speed: 28.8 Kbps (if you're lucky)",
        "Loading... Please wait... Loading... Please wait...",
        "Sending SYN... waiting for SYN-ACK...",
        "ACK received. TCP handshake complete.",
        "Retransmitting packet 0x4F...",
        "Carrier wave detected. Modulating signal...",
        "Checksum mismatch on packet 0x42. Requesting resend.",
        "Error correcting... retransmitting block 7 of 12",
        "Compression: V.42bis enabled",
        "ECM (Error Correction Mode) engaged",
        "Fallback negotiated: 14400 bps",
        "Carrier lost — renegotiating handshake",
        "AT&F — restoring factory defaults",
        "ATH — line disconnect requested",
        "Remote carrier capacity: 33600 bps max"
    };

    // Gaming memes
    public static readonly string[] Gaming =
    {
        "All your base are belong to us",
        "SNAKE? SNAKE?! SNAAAAAAAAAAAAKE!",
        "Hadouken!",
        "It's dangerous to go alone! Take this.",
        "The cake is a lie",
        "Would you kindly... connect?",
        "Finish the fight",
        "Wake me when you need me to connect",
        "War... war never changes",
        "Hey! Listen!",
        "It's-a me, Xbox!",
        "Do a barrel roll!",
        "Press X to doubt",
        "Press F to pay respects to your connection",
        "FUS RO DAH!",
        "Stop right there, criminal scum!",
        "I took an arrow to the knee",
        "A winner is you!",
        "Somebody set up us the bomb",
        "For great justice",
        "You have died of dysentery",
        "The right man in the wrong place can make all the difference",
        "Stay awhile and listen",
        "You must construct additional pylons",
        "I need more minerals",
        "GG WP",
        "Git gud at connecting",
        "Wololo",
        "Power overwhelming",
        "There is no cow level"
    };

    // Internet / classic memes
    public static readonly string[] Internet =
    {
        "I'm in.",
        "This is fine.",
        "One does not simply connect to Xbox",
        "I don't always test connections, but when I do...",
        "Connection failed successfully",
        "It's not a bug, it's a feature",
        "Have you tried turning it off and on again?",
        "It works on my machine™",
        "The answer is 42",
        "What could possibly go wrong?",
        "Hold my beer",
        "Challenge accepted",
        "Trust me, I'm an engineer",
        "According to my calculations, this should work",
        "Famous last words: it should be fine",
        "Error: Cat detected on keyboard",
        "BRB, coffee needed",
        "Plot twist: it works",
        "To be continued..."
    };

    // Tech references
    public static readonly string[] Tech =
    {
        "Pinging 127.0.0.1... ok.",
        "DNS resolution: 3ms (miraculously)",
        "MTU: 1492",
        "Checksum: OK",
        "Traceroute: 17 hops and a dream",
        "Cache miss. Fetching from mainframe...",
        "Quantum tunneling enabled",
        "Encryption: AES-256 (take that, NSA)",
        "Firewall: allow all (we trust everyone)",
        "Connection secured via hopes and prayers",
        "Toggle flux capacitor... engaged",
        "Signal-to-noise ratio: acceptable (barely)",
        "Download: ███░░░░░ 37%, ETA: ∞",
        "Upload: just a few bytes, promise",
        "Port knocking sequence: 1, 2, 3, 4, 5? That's the combination on my luggage!",
        "Sending SYN... SYN-ACK received",
        "ACK sent. Handshake complete.",
        "Latency: 999ms (routing via Mars)",
        "Jitter: yes",
        "TCP window size: 65535 bytes",
        "NAT type: Strict (good luck)",
        "UPnP: not found",
        "DHCP lease renewed. New IP: 192.168.0.42",
        "ARP cache flushed",
        "IPv6: link-local only (classic)",
        "SSL/TLS handshake: certificate verified",
        "HTTP/2 connection multiplexed",
        "WebSocket upgrade: 101 Switching Protocols",
        "DNS cache poisoned (just kidding... unless?)",
        "NetBIOS over TCP/IP: enabled",
        "WINS server registration failed (not that anyone uses WINS)",
        "Wake-on-LAN packet sent to FF:FF:FF:FF:FF:FF",
        "SSDP discovery: Xbox found on network",
        "ICMP echo reply received in 42ms"
    };

    public static readonly string[] All;

    static MemeLines()
    {
        var combined = new List<string>();
        combined.AddRange(DialUp);
        combined.AddRange(Gaming);
        combined.AddRange(Internet);
        combined.AddRange(Tech);
        All = combined.ToArray();
    }
}
