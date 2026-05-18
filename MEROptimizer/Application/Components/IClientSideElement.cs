using LabApi.Features.Wrappers;
using UnityEngine;

namespace MEROptimizer.Application.Components
{
  public interface IClientSideElement
  {
    Vector3 scale { get; }
    Vector3 position { get; }

    void SpawnClient(Player player);
    void SpawnForEveryone();
    void DestroyClient(Player player);
    void DestroyForEveryone();
  }
}
