using Unity.Collections;
using Unity.Entities;

namespace Netcode.Commands
{
    public interface ISerializableCommand<T> where T : struct, INetworkCommand
    {
        public void Serialize(ref DataStreamWriter writer, T data);
        public void Deserialize(ref DataStreamReader reader, T data);
        
        PortableFunctionPointer<NetworkCommandCollection.CreateDelegate> CompileExecute();
    }

    public interface INetworkCommand : IComponentData { }
}