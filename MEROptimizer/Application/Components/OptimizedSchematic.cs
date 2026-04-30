using System;
using System.Collections.Generic;
using System.Linq;
using LabApi.Features.Wrappers;
using MEC;
using PlayerRoles;
using ProjectMER.Features.Objects;
using UnityEngine;

namespace MEROptimizer.Application.Components
{
  public class OptimizedSchematic
  {
    // ask Math why, do not edit else it break
    public const float YOffsetForClusterSpawn = 2000;

    public SchematicObject schematic { get; set; }

    private string schematicName;

    public List<Collider> colliders { get; set; }

    public List<IClientSideElement> nonClusteredElement { get; set; }

    public List<ElementCluster> elementClusters { get; set; }

    public DateTime spawnTime { get; set; }

    public int schematicServerEmptiesElementSideCount { get; set; } = -1;

    public int schematicServerSideElmentCount { get; set; } = -1;

    public float visualDistance { get; set; } 

    public int GetTotalElementCount()
    {
      int count = nonClusteredElement.Count;

      foreach (ElementCluster cluster in elementClusters)
      {
        count += cluster.elements.Count;
      }

      return count;
    }

    public OptimizedSchematic(SchematicObject schematic, List<Collider> colliders, Dictionary<IClientSideElement, bool> elements,
      bool doClusters = false, float distance = 50, List<string> excludedUnspawnObjects = null, float maxDistanceForElementCluster = 2.5f,
      int maxElementsPerCluster = 100)
    {
      this.schematic = schematic;
      this.colliders = colliders;
      this.visualDistance = distance;
      spawnTime = DateTime.Now;

      schematicName = schematic.name;

      nonClusteredElement = new List<IClientSideElement>();
      elementClusters = new List<ElementCluster>();

      GenerateClustersAndSpawn(doClusters, elements, distance, excludedUnspawnObjects, maxDistanceForElementCluster, maxElementsPerCluster);
    }

    private void GenerateClustersAndSpawn(bool doClusters, Dictionary<IClientSideElement, bool> elements,
      float distance, List<string> excludedUnspawnObjects, float maxDistanceForElementCluster, int maxElementsPerCluster)
    {
      if (!doClusters)
      {
        foreach (IClientSideElement element in elements.Keys)
        {
          nonClusteredElement.Add(element);
        }
      }
      else
      {

        // Remove non clustered primitives and big objects
        foreach (IClientSideElement element in elements.Keys.ToList())
        {
          if (!elements[element])
          {
            nonClusteredElement.Add(element);
            elements.Remove(element);
          }
          else
          {
            if (MEROptimizer.MinimumSizeBeforeBeingBigElement > 0)
            {
              Vector3 size = element.scale;

              if (Math.Abs(size.x) + Math.Abs(size.y) + Math.Abs(size.z) > MEROptimizer.MinimumSizeBeforeBeingBigElement)
              {
                nonClusteredElement.Add(element);
                elements.Remove(element);
              }
            }
          }
        }

        if (!elements.IsEmpty())
        {
          // Calculate the center of the schematic, where the first cluster will spawn
          Vector3 center3D = Vector3.zero;
          foreach (IClientSideElement p in elements.Keys)
          {
            center3D += p.position;
          }

          center3D /= elements.Count;

          // Sort the elements by their distance with the center
          List<IClientSideElement> sortedElements = elements.Keys.ToList();
          sortedElements = sortedElements.OrderBy(s => Vector3.Distance(s.position, center3D)).ToList();

          Dictionary<int, List<IClientSideElement>> clusters = new Dictionary<int, List<IClientSideElement>>();

          int clusterNumber = 1;

          // Creates clusters, add the elements to the clusters until all clusters are generated
          while (sortedElements.Count > 0)
          {

            IClientSideElement closestFromCenterElement = sortedElements.First();

            List<IClientSideElement> clusterElements = new List<IClientSideElement>() { closestFromCenterElement };

            List<IClientSideElement> sortedElementByCluster = sortedElements.ToList();

            Vector3 centerPos = closestFromCenterElement.position;

            // Keep all of the elements where their distance correspond
            sortedElementByCluster.RemoveAll(p =>
            Vector3.Distance(p.position, centerPos) > maxDistanceForElementCluster);

            // Remove excess elements based on config
            if (sortedElementByCluster.Count > maxElementsPerCluster)
            {
              sortedElementByCluster = sortedElementByCluster.OrderBy(s => Vector3.Distance(s.position, centerPos)).ToList();
              sortedElementByCluster.RemoveRange(maxElementsPerCluster, sortedElementByCluster.Count - maxElementsPerCluster);
            }


            clusterElements.AddRange(sortedElementByCluster);

            sortedElements.RemoveAll(p => clusterElements.Contains(p));

            // sort the elements on their y value, so that the first to spawn will be the bottom ones
            clusterElements = clusterElements.OrderBy(p => p.position.y).ToList();

            clusters.Add(clusterNumber++, clusterElements);
          }

          //Creates the Gameobjects for the clusters
          foreach (KeyValuePair<int, List<IClientSideElement>> cluster in clusters)
          {
            // Get the center of the cluster

            Vector3 center = Vector3.zero;
            foreach (IClientSideElement element in cluster.Value)
            {
              center += element.position;
            }

            center /= cluster.Value.Count;

            // Creates the GameObject

            GameObject gameObject = new GameObject($"[MERO] PrimitiveCluster_{schematic.name}_{cluster.Key}");

            gameObject.transform.position = center + new Vector3(0, YOffsetForClusterSpawn, 0);
            gameObject.transform.rotation = Quaternion.identity;
            gameObject.transform.localScale = Vector3.one;

            SphereCollider collider = gameObject.AddComponent<SphereCollider>();
            collider.radius = distance;
            collider.isTrigger = true;

            ElementCluster elementCluster = gameObject.AddComponent<ElementCluster>();
            elementCluster.id = cluster.Key;
            elementCluster.elements = cluster.Value;

            elementClusters.Add(elementCluster);
          }
        }
      }

      // Spawn of elements
      foreach (IClientSideElement element in nonClusteredElement)
      {
        element.SpawnForEveryone();
      }


      // Spawn clusters for custom chiantos roles

      Timing.CallDelayed(.5f, () =>
      {

        if (this == null) return;

        foreach (Player player in Player.List.Where(p => p != null && !p.IsNpc))
        {
          bool shouldSpawn = false;

          // Tutorials if config is enabled
          if (!Application.MEROptimizer.ShouldTutorialsBeAffectedByDistanceSpawning && player.Role == RoleTypeId.Tutorial)
          {
            shouldSpawn = true;
          }

          // Spectators if config is enabled
          if (!Application.MEROptimizer.shouldSpectatorsBeAffectedByPDS && (player.Role == RoleTypeId.Spectator || player.Role == RoleTypeId.Overwatch))
          {
            shouldSpawn = true;
          }

          // Theses role always see all of the maps
          if (player.Role == RoleTypeId.Filmmaker || player.Role == RoleTypeId.Scp079)
          {
            shouldSpawn = true;
          }


          if (shouldSpawn)
          {
            foreach (ElementCluster cluster in elementClusters)
            {
              if (cluster.instantSpawn)
              {
                cluster.SpawnFor(player);
              }
              else
              {
                cluster.awaitingSpawn.Remove(player);
                cluster.awaitingSpawn.Add(player, cluster.elements.ToList());
                cluster.spawning = true;
              }
            }

          }
        }
      });
    }

    public void RefreshFor(Player player)
    {
      HideFor(player, false);

      foreach (IClientSideElement element in nonClusteredElement)
      {
        element.SpawnClient(player);
      }
      MEROptimizer.Debug($"Refresh the schematic {this.schematicName} for {player.DisplayName} !");
    }

    public void HideFor(Player player, bool showDebug = true)
    {
      if (player == null) return;
      if (showDebug)
      {
        MEROptimizer.Debug($"Hiding client side elements of {this.schematicName} to {player.DisplayName}");
      }

      foreach (IClientSideElement element in nonClusteredElement)
      {
        element.DestroyClient(player);
      }
    }

    public void SpawnClientElementsToAll()
    {
      MEROptimizer.Debug($"Displaying {schematicName}'s client side element !");
      foreach (Player player in Player.List.Where(p => p != null && !p.IsNpc))
      {
        SpawnClientElements(player);
      }
    }

    public void SpawnClientElements(Player player)
    {
      if (player == null) return;

      MEROptimizer.Debug($"Displaying client side elements of {this.schematicName} to {player.DisplayName}");
      foreach (IClientSideElement element in nonClusteredElement)
      {
        element.SpawnClient(player);
      }
    }

    public void Destroy()
    {
      foreach (Collider collider in colliders.Where(c => c != null && c.gameObject != null))
      {
        UnityEngine.Object.Destroy(collider);
      }

      foreach (IClientSideElement primitive in nonClusteredElement)
      {
        primitive.DestroyForEveryone();
      }

      foreach (ElementCluster cluster in elementClusters.Where(c => c != null && c.gameObject != null))
      {
        UnityEngine.Object.Destroy(cluster);
      }

      MEROptimizer.Debug($"Destroyed client side schematic of {schematicName} !");
    }
  }
}
