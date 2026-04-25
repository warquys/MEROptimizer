using System.Linq;
using AdminToys;
using LabApi.Features.Wrappers;
using MEROptimizer.MEROptimizer.Application.Components;
using Mirror;
using UnityEngine;

namespace MEROptimizer.Application.Components
{
  public class ClientSidePrimitive : IClientSideElement
  {
    // Mirror Payload Header specific for the target behavour
    //  Depends on the order of the NetworkBehaviour on the Primitive Object Toy prefab.
    private const byte PrimitiveBehaviourIndex = 1;
    private const byte PrimitiveInitialPayloadSize = 67;

    private const byte DefaultMovementSmoothing = 0;
    private const bool DefaultIsStatic = false;
    private const uint NoParentNetId = 0;

    public Vector3 position { get; set; }
    public Quaternion rotation { get; set; }
    public Vector3 scale { get; set; }
    public PrimitiveType primitiveType { get; set; }
    public Color color { get; set; }
    public PrimitiveFlags primitiveFlags { get; set; }

    public SpawnMessage spawnMessage { get; set; }

    public ObjectDestroyMessage destroyMessage { get; set; }

    public uint netId { get; set; }

    public ClientSidePrimitive(AdminToys.PrimitiveObjectToy primitive) : this(
      primitive.transform.position,
      primitive.transform.rotation,
      primitive.transform.localScale,
      primitive.PrimitiveType,
      primitive.NetworkMaterialColor,
      primitive.PrimitiveFlags
      ) { }

    public ClientSidePrimitive(
        Vector3 position,
        Quaternion rotation,
        Vector3 scale,
        PrimitiveType primitiveType,
        Color color,
        PrimitiveFlags primitiveFlags)
    {
      this.position = position;
      this.rotation = rotation;
      this.scale = scale;
      this.primitiveType = primitiveType;
      this.color = color;
      this.primitiveFlags = primitiveFlags;

      netId = NetworkIdentity.GetNextNetworkId();
      GenerateNetworkMessages();
    }

    private void GenerateNetworkMessages()
    {
      NetworkWriterPooled writer = NetworkWriterPool.Get();

      // 1) Header: behaviour index + payload size
      writer.Write<byte>(PrimitiveBehaviourIndex);
      writer.Write<byte>(PrimitiveInitialPayloadSize);

      // 2) AdminToyBase.SerializeSyncVars(forceAll: true)
      writer.Write<Vector3>(position);
      writer.Write<Quaternion>(rotation);
      writer.Write<Vector3>(scale);
      writer.Write<byte>(DefaultMovementSmoothing);
      writer.Write<bool>(DefaultIsStatic);

      // 3) PrimitiveObjectToy.SerializeSyncVars(forceAll: true)
      writer.Write<int>((int)primitiveType);
      writer.Write<Color>(color);
      writer.Write<byte>((byte)primitiveFlags);

      // 4) AdminToyBase.OnSerialize(initialState: true) -> parent netId
      writer.Write<uint>(NoParentNetId);

      spawnMessage = new SpawnMessage()
      {
        netId = netId,
        isLocalPlayer = false,
        isOwner = false,
        sceneId = 0,
        assetId = MEROptimizer.PrimitiveAssetId,
        position = position,
        rotation = rotation,
        scale = scale,
        payload = writer.ToArraySegment()
      };

      destroyMessage = new ObjectDestroyMessage()
      {
        netId = netId,
      };
    }

    public void DestroyForEveryone()
    {
      foreach (Player player in Player.List.Where(p => p != null && !p.IsNpc && !p.IsDummy))
      {
        DestroyClient(player);
      }
    }

    public void DestroyClient(Player target)
    {
      if (target == null || target.IsHost) return; // DO NOT SEND THIS TO THE DEDICATED OTHERWISE EVERYTHING WILL BROKE TRUST ME I LOST 3 MONTHS OF MY LIFE BECAUSE OF THIS

      target.Connection?.Send(destroyMessage);
    }

    public void SpawnForEveryone()
    {
      foreach (Player player in Player.List.Where(p => p != null && !p.IsNpc && !p.IsDummy))
      {
        SpawnClient(player);
      }
    }

    public void SpawnClient(Player target)
    {
      if (target == null || target.IsHost) return; // DO NOT SEND THIS TO THE DEDICATED OTHERWISE EVERYTHING WILL BROKE TRUST ME I LOST 3 MONTHS OF MY LIFE BECAUSE OF THIS

      target.Connection?.Send(spawnMessage);
    }
  }
}
