using System.Linq;
using LabApi.Features.Wrappers;
using MEROptimizer.MEROptimizer.Application.Components;
using Mirror;
using System;
using UnityEngine;

namespace MEROptimizer.Application.Components
{
  public class ClientSideLight : IClientSideElement
  {
    // Mirror Payload Header specific for the target behavour
    //  Depends on the order of the NetworkBehaviour on the Primitive Object Toy prefab.
    private const byte PrimitiveBehaviourIndex = 1;
    private const byte PrimitiveInitialPayloadSize = 90;

    const byte movementSmoothing = 0;
    const bool isStatic = false;
    private const uint NoParentNetId = 0;

    public Vector3 position { get; set; }
    public Quaternion rotation { get; set; }
    public Vector3 scale { get; set; }

    public float lightIntensity { get; set; }
    public float lightRange { get; set; }
    public Color lightColor { get; set; }
    public LightShadows shadowType { get; set; }
    public float shadowStrength { get; set; }
    public LightType lightType { get; set; }
    public LightShape lightShape { get; set; }
    public float spotAngle { get; set; }
    public float innerSpotAngle { get; set; }

    public SpawnMessage spawnMessage { get; set; }

    public ObjectDestroyMessage destroyMessage { get; set; }

    public uint netId { get; set; }

    public ClientSideLight(AdminToys.LightSourceToy light) : this(
        light.transform.position,
        light.transform.rotation,
        light.transform.localScale,
        light.LightIntensity,
        light.LightRange,
        light.LightColor,
        light.ShadowType,
        light.ShadowStrength,
        light.LightType,
        light.LightShape,
        light.SpotAngle,
        light.InnerSpotAngle) { }

    public ClientSideLight(
      Vector3 position,
      Quaternion rotation,
      Vector3 scale,
      float lightIntensity,
      float lightRange,
      Color lightColor,
      LightShadows shadowType,
      float shadowStrength,
      LightType lightType,
      LightShape lightShape,
      float spotAngle,
      float innerSpotAngle)
    {
      this.position = position;
      this.rotation = rotation;
      this.scale = scale;

      this.lightIntensity = lightIntensity;
      this.lightRange = lightRange;
      this.lightColor = lightColor;
      this.shadowType = shadowType;
      this.shadowStrength = shadowStrength;
      this.lightType = lightType;
      this.lightShape = lightShape;
      this.spotAngle = spotAngle;
      this.innerSpotAngle = innerSpotAngle;

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
      writer.Write<byte>(movementSmoothing);
      writer.Write<bool>(isStatic);

      // 3) PrimitiveObjectToy.SerializeSyncVars(forceAll: true)
      writer.Write<float>(lightIntensity);
      writer.Write<float>(lightRange);
      writer.Write<Color>(lightColor);
      writer.Write<int>((int)shadowType);
      writer.Write<float>(shadowStrength);
      writer.Write<int>((int)lightType);
      writer.Write<int>((int)lightShape);
      writer.Write<float>(spotAngle);
      writer.Write<float>(innerSpotAngle);

      // 4) AdminToyBase.OnSerialize(initialState: true) -> parent netId
      writer.Write<uint>(NoParentNetId);

      spawnMessage = new SpawnMessage()
      {
        netId = netId,
        isLocalPlayer = false,
        isOwner = false,
        sceneId = 0,
        assetId = MEROptimizer.LightAssetId,
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
