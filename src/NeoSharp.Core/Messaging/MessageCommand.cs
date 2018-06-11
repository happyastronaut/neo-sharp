using NeoSharp.Core.Caching;
using NeoSharp.Core.Messaging.Messages;

namespace NeoSharp.Core.Messaging
{
    public enum MessageCommand : byte
    {
        //notfound = 0x00,

        [ReflectionCache(typeof(AddrMessage))]
        addr = 0x01,
        //alert = 0x02,
        block = 0x03,
        consensus = 0x04,
        filteradd = 0x05,
        filterclear = 0x06,
        filterload = 0x07,
        [ReflectionCache(typeof(GetAddrMessage))]
        getaddr = 0x08,
        [ReflectionCache(typeof(GetBlockHashesMessage))]
        getblocks = 0x09,
        getdata = 0x0A,
        [ReflectionCache(typeof(GetBlockHeadersMessage))]
        getheaders = 0x0B,
        [ReflectionCache(typeof(BlockHeadersMessage))]
        headers = 0x0C,
        [ReflectionCache(typeof(InventoryMessage))]
        inv = 0x0D,
        [ReflectionCache(typeof(MemPoolMessage))]
        mempool = 0x0E,
        //merkleblock = 0x0F,
        //ping = 0x10,
        //pong = 0x11,
        //reject = 0x12,
        tx = 0x13,
        [ReflectionCache(typeof(VerAckMessage))]
        verack = 0x14,
        [ReflectionCache(typeof(VersionMessage))]
        version = 0x15,
    }
}