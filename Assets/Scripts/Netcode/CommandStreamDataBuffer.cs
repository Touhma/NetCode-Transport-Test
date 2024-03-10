using Unity.Entities;

namespace Netcode
{
    [InternalBufferCapacity(0)]
    public struct OutgoingNetworkDataStreamBuffer : IBufferElementData
    {
        public byte Value;
    }
    [InternalBufferCapacity(0)]
    public struct IncomingNetworkDataStreamBuffer : IBufferElementData
    {
        public byte Value;
    }
}