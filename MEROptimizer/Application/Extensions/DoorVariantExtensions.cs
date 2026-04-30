using System;
using System.Linq;
using Interactables.Interobjects.DoorUtils;
using Mirror;
using ProjectMER.Features.Enums; // Mettez l'espace de noms correct pour DoorType si nécessaire

namespace MEROptimizer.Application.Extensions
{
  public static class DoorVariantExtensions
  {
    public static DoorType GetDoorType(this DoorVariant door)
    {
      if (door == null)
        throw new System.ArgumentNullException(nameof(door));

      if (!door.TryGetComponent<NetworkIdentity>(out var networkIdentity))
        throw new System.InvalidOperationException("DoorVariant does not have a NetworkIdentity component.");

      uint assetId = networkIdentity.assetId;

      foreach (var kvp in MEROptimizer.DoorAssetIds)
      {
        if (kvp.Value == assetId)
        {
          return kvp.Key;
        }
      }

      throw new NotImplementedException("Type of door not implemented.");
    }
  }
}