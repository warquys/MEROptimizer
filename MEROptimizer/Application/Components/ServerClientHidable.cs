using System.Linq;
using LabApi.Features.Wrappers;
using MEROptimizer.Application.Components;
using Mirror;
using UnityEngine;

namespace MEROptimizer.MEROptimizer.Application.Components
{
  public class ServerClientHidable : IClientSideElement
  {
    public Transform transform;
    public NetworkIdentity networkIdentity;

    public Vector3 scale { get => transform.localScale; set => transform.localScale = value; }
    public Vector3 position { get => transform.position; set => transform.position = value; }

    public ServerClientHidable(Transform transform, NetworkIdentity networkIdentity)
    {
      this.transform = transform;
      this.networkIdentity = networkIdentity;
      networkIdentity.visible = Visibility.Default;
    }

    public void DestroyClient(Player player)
    {
      NetworkServer.HideForConnection(networkIdentity, player.Connection);
    }

    public void DestroyForEveryone()
    {
      foreach (Player player in Player.List.Where(p => p != null && !p.IsNpc && !p.IsDummy))
      {
        DestroyClient(player);
      }
    }

    public void SpawnClient(Player player)
    {
      NetworkServer.ShowForConnection(networkIdentity, player.Connection);
    }

    public void SpawnForEveryone()
    {
      foreach (Player player in Player.List.Where(p => p != null && !p.IsNpc && !p.IsDummy))
      {
        SpawnClient(player);
      }
    }
  }
}
