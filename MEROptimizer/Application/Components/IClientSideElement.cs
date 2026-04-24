using LabApi.Features.Wrappers;
using UnityEngine;

namespace MEROptimizer.MEROptimizer.Application.Components
{
  public interface IClientSideElement
  {
    Vector3 scale { get; set; }

    Vector3 position { get; set; }

    void SpawnClient(Player player);
    void SpawnForEveryone();
    void DestroyClient(Player player);
    void DestroyForEveryone();
  }
}
