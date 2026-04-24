using LabApi.Features.Wrappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace MEROptimizer.Application.Components
{
  public class PlayerDisplayHint : MonoBehaviour
  {

    public Player player;

    private float timePassed = 0;

    public void RemoveComponent()
    {
      Destroy(this);
    }
    public void Update()
    {
      timePassed += Time.deltaTime; // mrc serious
      if (timePassed > .3f)
      {
        timePassed = 0;


        int count = 0;
        int totalPrimitiveCount = 0;

        foreach (OptimizedSchematic schematic in Plugin.merOptimizer.optimizedSchematics)
        {
          count += schematic.nonClusteredElement.Count;
          totalPrimitiveCount += (schematic.schematicServerSideElmentCount + schematic.nonClusteredElement.Count);
          foreach (ElementCluster cluster in schematic.elementClusters)
          {
            if (cluster.insidePlayers.Contains(player))
            {
              count += cluster.elements.Count;
            }

            totalPrimitiveCount += cluster.elements.Count;
          }
        }

        if (player == null)
        {
          Destroy(this);
          return;
        }

        player.SendHint($"Loaded <color=green>{count}</color> out of a total of <color=red>{totalPrimitiveCount}</color> primitives", .5f);

      }
    }
  }
}
