using System.Linq;
using Interactables.Interobjects.DoorUtils;
using Mirror;
using ProjectMER.Features.Enums;
using UnityEngine;
using LabApi.Features.Wrappers;

namespace MEROptimizer.Application.Components
{
  public class ClientSideDoor : IClientSideElement
  {
    // Mirror Payload Header specific for the target behavour
    //  Depends on the order of the NetworkBehaviour on the Object Toy prefab.
    private const byte DoorBehaviourIndex = 1;
    private const byte DoorInitialPayloadSize = 60;

    public DoorType doorType { get; set; }
    public Vector3 position { get; set; }
    public Quaternion rotation { get; set; }
    public Vector3 scale { get; set; }

    public bool targetState { get; set; }
    public DoorLockReason activeLocks { get; set; }
    public byte doorId { get; set; }

    public SpawnMessage spawnMessage { get; set; }
    public ObjectDestroyMessage destroyMessage { get; set; }
    public uint netId { get; set; }

    public ClientSideDoor(DoorVariant door, DoorType type) : this(
        door.transform.position,
        door.transform.rotation,
        door.transform.localScale,
        door.TargetState,
        (DoorLockReason)door.ActiveLocks,
        door.DoorId,
        type)
    { }

    public ClientSideDoor(
        Vector3 position,
        Quaternion rotation,
        Vector3 scale,
        bool targetState,
        DoorLockReason activeLocks,
        byte doorId,
        DoorType doorType)
    {
      this.position = position;
      this.rotation = rotation;
      this.scale = scale;
      this.targetState = targetState;
      this.activeLocks = activeLocks;
      this.doorType = doorType;
      this.doorId = doorId;

      netId = NetworkIdentity.GetNextNetworkId();
      GenerateNetworkMessages(doorType);
    }

    private void GenerateNetworkMessages(DoorType doorType)
    {
      using (var writer = NetworkWriterPool.Get())
      {
        var doorAssetId = MEROptimizer.DoorAssetIds[doorType];
        // 1) Header
        writer.Write<byte>(DoorBehaviourIndex);
        writer.Write<byte>(DoorInitialPayloadSize);

        // 2) DoorVariant.SerializeSyncVars(forceAll: true)
        writer.Write<bool>(targetState);
        writer.Write<ushort>((ushort)activeLocks);
        writer.Write<byte>(doorId);

        spawnMessage = new SpawnMessage()
        {
          netId = netId,
          isLocalPlayer = false,
          isOwner = false,
          sceneId = 0,
          assetId = doorAssetId,
          position = position,
          rotation = rotation,
          scale = scale,
          payload = writer.ToArray().Segment(0, (int)writer.Position)
        };

        destroyMessage = new ObjectDestroyMessage()
        {
          netId = netId,
        };
      }
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
      if (target == null || target.IsHost) return;
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
      if (target == null || target.IsHost) return;
      target.Connection?.Send(spawnMessage);
    }
  }
}