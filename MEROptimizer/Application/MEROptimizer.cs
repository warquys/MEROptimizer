using System;
using System.Collections.Generic;
using System.Linq;
using AdminToys;
using Exiled.API.Features.Doors;
using Interactables.Interobjects.DoorUtils;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.Scp079Events;
using LabApi.Features.Wrappers;
using MEC;
using MEROptimizer.Application.Components;
using MEROptimizer.Application.Extensions;
using Mirror;
using PlayerRoles;
using ProjectMER.Events.Arguments;
using ProjectMER.Features;
using UnityEngine;
using DoorType = ProjectMER.Features.Enums.DoorType;
using LightSourceToy = AdminToys.LightSourceToy;
using Logger = LabApi.Features.Console.Logger;
using PrimitiveObjectToy = AdminToys.PrimitiveObjectToy;

#if EXILED
using Exiled.Events.EventArgs.Player;
#endif
namespace MEROptimizer.Application
{
  public class MEROptimizer
  {
    public static uint PrimitiveAssetId;
    public static uint LightAssetId;
    public static Dictionary<DoorType, uint> DoorAssetIds = new Dictionary<DoorType, uint>();

    private bool excludeCollidables;

    private List<string> excludedNames;

    private bool hideDistantPrimitives;

    public static bool shouldSpectatorsBeAffectedByPDS;

    public static bool ShouldTutorialsBeAffectedByDistanceSpawning;

    private float distanceRequiredForUnspawning;

    private Dictionary<string, float> CustomSchematicSpawnDistance = new Dictionary<string, float>();

    private float maxDistanceForPrimitiveCluster;

    private int maxPrimitivesPerCluster;

    private List<string> excludedNamesForUnspawningDistantObjects;

    public static float numberOfPrimitivePerSpawn;

    public static float MinimumSizeBeforeBeingBigElement;

    public static bool isDynamiclyDisabled = false;

    public static bool IsDebug = false;

    public List<OptimizedSchematic> optimizedSchematics = new List<OptimizedSchematic>();
    public void Load(Config config)
    {
      MEROptimizer.IsDebug = config.Debug;
      excludeCollidables = config.OptimizeOnlyNonCollidable;

      //temp
      excludedNames = new List<string>();
      foreach (string name in config.excludeObjects)
      {
        excludedNames.Add(name.ToLower());
      }

      hideDistantPrimitives = config.ClusterizeSchematic;
      distanceRequiredForUnspawning = config.SpawnDistance;
      excludedNamesForUnspawningDistantObjects = config.excludeUnspawningDistantObjects;
      maxDistanceForPrimitiveCluster = config.MaxDistanceForPrimitiveCluster;
      maxPrimitivesPerCluster = config.MaxPrimitivesPerCluster;
      shouldSpectatorsBeAffectedByPDS = config.ShouldSpectatorBeAffectedByDistanceSpawning;
      numberOfPrimitivePerSpawn = config.numberOfPrimitivePerSpawn;
      MinimumSizeBeforeBeingBigElement = config.MinimumSizeBeforeBeingBigPrimitive;
      ShouldTutorialsBeAffectedByDistanceSpawning = config.ShouldTutorialsBeAffectedByDistanceSpawning;
      CustomSchematicSpawnDistance = config.CustomSchematicSpawnDistance;

#if EXILED
      Exiled.Events.Handlers.Player.Verified += OnVerified;
      Exiled.Events.Handlers.Player.Spawned += OnSpawned;
      Exiled.Events.Handlers.Player.ChangingSpectatedPlayer += OnChangingSpectatedPlayer;
      Exiled.Events.Handlers.Server.WaitingForPlayers += OnWaitingForPlayers;
      Exiled.Events.Handlers.Scp079.ChangingCamera += OnChangingCamera;
#else
      // LabAPI Events
      LabApi.Events.Handlers.PlayerEvents.Joined += OnJoined;
      LabApi.Events.Handlers.PlayerEvents.Spawned += OnSpawned;
      LabApi.Events.Handlers.PlayerEvents.ChangedSpectator += OnChangedSpectator;
      LabApi.Events.Handlers.ServerEvents.WaitingForPlayers += OnWaitingForPlayers;
      LabApi.Events.Handlers.Scp079Events.ChangedCamera += OnScp079ChangedCamera;
#endif


      // MER Events
      ProjectMER.Events.Handlers.Schematic.SchematicSpawned += OnSchematicSpawned;
      ProjectMER.Events.Handlers.Schematic.SchematicDestroyed += OnSchematicDestroyed;

    }

    public void Unload()
    {
#if EXILED
      Exiled.Events.Handlers.Player.Verified += OnVerified;
      Exiled.Events.Handlers.Player.Spawned += OnSpawned;
      Exiled.Events.Handlers.Player.ChangingSpectatedPlayer += OnChangingSpectatedPlayer;
      Exiled.Events.Handlers.Server.WaitingForPlayers += OnWaitingForPlayers;
      Exiled.Events.Handlers.Scp079.ChangingCamera -= OnChangingCamera;
#else
      // LabAPI Events
      LabApi.Events.Handlers.PlayerEvents.Joined += OnJoined;
      LabApi.Events.Handlers.PlayerEvents.Spawned += OnSpawned;
      LabApi.Events.Handlers.PlayerEvents.ChangedSpectator += OnChangedSpectator;
      LabApi.Events.Handlers.ServerEvents.WaitingForPlayers += OnWaitingForPlayers;
      LabApi.Events.Handlers.Scp079Events.ChangedCamera -= OnScp079ChangedCamera;
#endif

      // MER Events

      ProjectMER.Events.Handlers.Schematic.SchematicSpawned -= OnSchematicSpawned;
      ProjectMER.Events.Handlers.Schematic.SchematicDestroyed -= OnSchematicDestroyed;

      Clear();
    }


    // ---------------------- Private methods

    public static void Debug(string message)
    {
      if (!MEROptimizer.IsDebug) return;

#if EXILED
      Exiled.API.Features.Log.Debug(message);
#else
      Logger.Debug(message);
#endif
    }

    private void Clear()
    {
      optimizedSchematics.Clear();
    }

    Dictionary<T, bool> GetToOptimize<T>(Transform root, List<Transform> parentToExclude)
      where T : MonoBehaviour
    {
      Dictionary<T, bool> elements = new Dictionary<T, bool>();
      InterGetToOptimize(root, true);
      return elements;
      
      void InterGetToOptimize(Transform current, bool clusterChilds)
      {
        for (int i = 0; i < current.childCount; i++)
        {
          Transform child = current.GetChild(i);
          if (child == null || parentToExclude.Contains(child)) continue;

          if (clusterChilds)
          {
            foreach (string name in excludedNamesForUnspawningDistantObjects)
            {
              if (child.name.Contains(name))
              {
                clusterChilds = false;
              }
            }
          }

          if (excludedNames.Any(n => child.name.ToLower().Contains(n.ToLower())))
          {
            continue;
          }

          if (child.TryGetComponent(out T element))
          {
            if (element is PrimitiveObjectToy primitive)
            {
              if (this.excludeCollidables && primitive.PrimitiveFlags.HasFlag(PrimitiveFlags.Collidable))
              {
                continue;
              }

              if (primitive.PrimitiveFlags != PrimitiveFlags.None)
              {
                 elements.Add(element, clusterChilds);
              }
            }
            else
            {
              elements.Add(element, clusterChilds);
            }
          }

          InterGetToOptimize(child, clusterChilds);
        }
      }
    }

    // --------------- EXILED/LabAPI Events

#if EXILED
    private void OnVerified(VerifiedEventArgs ev)
    {
      OnPlayerJoined(ev.Player);
    }

    private void OnSpawned(SpawnedEventArgs ev)
    {
      OnPlayerSpawned(ev.Player);
    }

    private void OnChangingSpectatedPlayer(ChangingSpectatedPlayerEventArgs ev)
    {
      if (ev.Player == null || ev.NewTarget == null) return;

      Player oldTarget = null;
      if (ev.OldTarget != null) oldTarget = ev.OldTarget;
      OnPlayerChangedSpectator(ev.Player, oldTarget, ev.NewTarget);
    }

    private void OnChangingCamera(ChangingCameraEventArgs ev)
    {
      Scp079ChangeCamera(ev.Camera.Position, ev.Player);
    }

#else
    private void OnJoined(PlayerJoinedEventArgs ev)
    {
      OnPlayerJoined(ev.Player);
    }

    private void OnSpawned(PlayerSpawnedEventArgs ev)
    {
      OnPlayerSpawned(ev.Player);
    }

    private void OnChangedSpectator(PlayerChangedSpectatorEventArgs ev)
    {
      OnPlayerChangedSpectator(ev.Player, ev.OldTarget, ev.NewTarget);
    }

    private void OnScp079ChangedCamera(Scp079ChangedCameraEventArgs ev)
    {
      Scp079ChangeCamera(ev.Camera.Position, ev.Player);
    }

#endif
    private void OnWaitingForPlayers()
    {
      Clear();

      if (PrimitiveAssetId != 0 && LightAssetId != 0 && DoorAssetIds.Count != 0) return;
      PrefabManager.RegisterPrefabs();

      try
      {
        PrimitiveAssetId = PrefabManager.PrimitiveObject.GetComponent<NetworkIdentity>().assetId;
        Logger.Debug("PrimitiveObjectToy AssetId successfully found via PrefabManager.");
      }
      catch (System.Exception ex)
      {
        Logger.Error("Could not find the PrimitiveObjectToy prefab! Client-side primitives will fail to spawn.\n" + ex.Message);
      }

      try
      {
        LightAssetId = PrefabManager.LightSource.GetComponent<NetworkIdentity>().assetId;
        Logger.Debug("LightSourceToy AssetId successfully found via PrefabManager.");
      }
      catch (System.Exception ex)
      {
        Logger.Error("Could not find the LightSourceToy prefab! Client-side lights will fail to spawn." + ex.Message);
      }

      DoorType[] spawnableDoors = new DoorType[]
      {
          DoorType.Lcz,
          DoorType.Hcz,
          DoorType.Ez,
          DoorType.HeavyBulkDoor,
          DoorType.Gate
      };

      foreach (DoorType doorType in spawnableDoors)
      {
        try
        {
          DoorVariant doorPrefab;
          switch (doorType)
          {
            case DoorType.Lcz:
              doorPrefab = PrefabManager.DoorLcz;
              break;
            case DoorType.Hcz:
              doorPrefab = PrefabManager.DoorHcz;
              break;
            case DoorType.Ez:
              doorPrefab = PrefabManager.DoorEz;
              break;
            case DoorType.Bulkdoor:
              doorPrefab = PrefabManager.DoorHeavyBulk;
              break;
            case DoorType.Gate:
              doorPrefab = PrefabManager.DoorGate;
              break;
            default:
              throw new NotImplementedException($"Type of doors {doorType} need to be register in OnWaitingForPlayers.");
          }

          DoorAssetIds[doorType] = doorPrefab.GetComponent<NetworkIdentity>().assetId;
          Logger.Debug($"DoorVariant AssetId for {doorType} successfully found.");
        }
        catch (System.Exception ex)
        {
          Logger.Error($"Could not find the DoorVariant prefab for {doorType}! Error: {ex.Message}");
        }
      }
    }

    //--------------- Events EXILED
    private void Scp079ChangeCamera(Vector3 newPos, Player scp)
    {
      // magic value, set in OptimizedSchematic, idk what that mean
      newPos += new Vector3(0, OptimizedSchematic.YOffsetForClusterSpawn, 0);
      foreach (OptimizedSchematic optimizedSchematic in Plugin.merOptimizer.optimizedSchematics)
      {
        foreach (ElementCluster cluster in optimizedSchematic.elementClusters)
        {
          if (Vector3.Distance(newPos, cluster.transform.position) <= optimizedSchematic.visualDistance * 20)
          {
            cluster.AddPlayer(scp);
          }
          else if (cluster.insidePlayers.Contains(scp))
          {
            cluster.RemovePlayer(scp);
          }
        }
      }
    }

    private void AddPlayerTrigger(Player player)
    {
      MEROptimizer.Debug($"Adding PlayerTrigger to {player.DisplayName}({player.PlayerId}) !");
      GameObject playerTrigger = new GameObject($"{player.PlayerId}_MERO_TRIGGER");
      playerTrigger.tag = "Player";

      Rigidbody rb = playerTrigger.AddComponent<Rigidbody>();
      rb.isKinematic = true;

      playerTrigger.AddComponent<BoxCollider>().size = new Vector3(1, 2, 1); // epic representation of a player's hitbox

      playerTrigger.AddComponent<PlayerTrigger>().player = player;

    }
    private void OnPlayerJoined(Player player)
    {
      if (player == null || player.IsNpc) return;

      AddPlayerTrigger(player);
      foreach (OptimizedSchematic schematic in optimizedSchematics.Where(s => s != null && s.schematic != null))
      {
        MEROptimizer.Debug($"Displaying static client sided primitives of {schematic.schematic.Name} to {player.DisplayName} because he just connected !");
        schematic.SpawnClientElements(player);
      }
    }

    // one of the worst code i've ever written, i'm sorry about that
    private void OnPlayerSpawned(Player player)
    {
      if (player == null) return;

      if (player.IsNpc)
      {
        bool hasFound = false;

        for (int i = 0; i < player.GameObject.transform.childCount; i++)
        {
          Transform child = player.GameObject.transform.GetChild(i);
          if (child != null && child.name == $"{player.PlayerId}_MERO_TRIGGER")
          {
            hasFound = true;
            break;
          }
        }

        if (!hasFound)
        {
          AddPlayerTrigger(player);
        }
      }
      else
      {

        // just spawned as a spectator, we spawn all clusters primitives for him
        if ((player.Role == RoleTypeId.Spectator || player.Role == RoleTypeId.Overwatch) && !shouldSpectatorsBeAffectedByPDS)
        {
          // Unspawning and then respawning primitives at the same frame causes the game to shit itself, so a delay is needed
          Timing.CallDelayed(.5f, () =>
          {
            if (player != null && (player.Role == RoleTypeId.Spectator || player.Role == RoleTypeId.Overwatch))
            {
              foreach (OptimizedSchematic schematic in optimizedSchematics.Where(s => s != null && s.schematic != null))
              {
                MEROptimizer.Debug($"Spawning all clusters (as a fade spawn) of {schematic.schematic.Name} to {player.DisplayName} because he spawned as a spectator (ssbadbs : {shouldSpectatorsBeAffectedByPDS})");

                foreach (ElementCluster cluster in schematic.elementClusters)
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
        if (!ShouldTutorialsBeAffectedByDistanceSpawning && player.Role == RoleTypeId.Tutorial)
        {
          Timing.CallDelayed(.5f, () =>
          {

            if (player != null && player.Role == RoleTypeId.Tutorial)
            {
              foreach (OptimizedSchematic schematic in optimizedSchematics.Where(s => s != null && s.schematic != null))
              {
                MEROptimizer.Debug($"Spawning all clusters (as a fade spawn) of {schematic.schematic.Name} to {player.DisplayName} because he spawned as a tutorial and based on the specified config he should see all of the map (ssbadbs : {shouldSpectatorsBeAffectedByPDS})");

                foreach (ElementCluster cluster in schematic.elementClusters)
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
        else
        {
          foreach (OptimizedSchematic schematic in optimizedSchematics)
          {
            MEROptimizer.Debug($"Unspawning all clusters of {schematic.schematic.Name} to {player.DisplayName} because he just changed role (ssbadbs : {shouldSpectatorsBeAffectedByPDS})");
            foreach (ElementCluster cluster in schematic.elementClusters)
            {
              if (!cluster.insidePlayers.Contains(player))
              {
                cluster.UnspawnFor(player);
              }

            }
          }

          if (player.Role == RoleTypeId.Filmmaker)
          {
            Timing.CallDelayed(.5f, () =>
            {
              if (player != null && (player.Role == RoleTypeId.Filmmaker))
              {
                foreach (OptimizedSchematic schematic in optimizedSchematics.Where(s => s != null && s.schematic != null))
                {
                  MEROptimizer.Debug($"Spawning all clusters (as a fade spawn) of {schematic.schematic.Name} to {player.DisplayName} because he spawned as a filmaker ( why ) and based on the specified config he should see all of the map (ssbadbs : {shouldSpectatorsBeAffectedByPDS})");

                  foreach (ElementCluster cluster in schematic.elementClusters)
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
          /*
          else if (player.Role != RoleTypeId.Scp079 && player.Role != RoleTypeId.Spectator)
          {
            
            Timing.CallDelayed(.1f, () =>
            {
              player.Position = player.Position + Vector3.up * 0.01f;
            });
          }
          */
        }
      }
    }

    private void OnPlayerChangedSpectator(Player player, Player oldTarget, Player newTarget)
    {
      if (!shouldSpectatorsBeAffectedByPDS) return;

      if (player == null || player.IsNpc || newTarget == null) return;

      foreach (OptimizedSchematic schematic in optimizedSchematics)
      {
        foreach (ElementCluster cluster in schematic.elementClusters)
        {
          if (oldTarget != null && (cluster.insidePlayers.Contains(oldTarget) && !cluster.insidePlayers.Contains(newTarget)))
          {
            cluster.UnspawnFor(player);
          }

          if (cluster.insidePlayers.Contains(newTarget) && (oldTarget == null || !cluster.insidePlayers.Contains(oldTarget)))
          {
            cluster.SpawnFor(player);
          }
        }
      }
    }

    // --------------- Events MER

    private void OnSchematicSpawned(SchematicSpawnedEventArgs ev)
    {
      if (isDynamiclyDisabled)
      {
        Logger.Warn($"Skipping the optimisation of {ev.Schematic.name} because the plugin is dynamicly disabled by command (mero.disable)");
        return;
      }

      if (ev.Schematic == null) return;

      if (excludedNames.Any(n => ev.Schematic.Name.ToLower().Contains(n)))
      {
        return;
      }

      List<Transform> parentsToExlude = new List<Transform>();

      foreach (Animator anim in ev.Schematic.GetComponentsInChildren<Animator>())
      {
        if (anim == null) continue;
        parentsToExlude.Add(anim.transform);
      }

      Dictionary<IClientSideElement, bool> clientSideElement = new Dictionary<IClientSideElement, bool>();
      List<Collider> serverSideColliders = new List<Collider>();
      List<GameObject> gameObjectToDestroy = new List<GameObject>();

      Dictionary<PrimitiveObjectToy, bool> primitivesToOptimize = GetToOptimize<PrimitiveObjectToy>(ev.Schematic.transform, parentsToExlude);

      if (primitivesToOptimize != null && !primitivesToOptimize.IsEmpty())
      {
        foreach (PrimitiveObjectToy primitive in primitivesToOptimize.Keys)
        {
          // store the data about the primitive
          clientSideElement.Add(new ClientSidePrimitive(primitive), primitivesToOptimize[primitive]);

          // Add collider for the server if the primitive is collidable
          if (primitive.PrimitiveFlags.HasFlag(PrimitiveFlags.Collidable))
          {
            GameObject collider = new GameObject();
            collider.transform.localScale = primitive.transform.lossyScale.Abs();
            collider.transform.position = primitive.transform.position;
            collider.transform.rotation = primitive.transform.rotation;
            collider.transform.name = $"[MEROCOLLIDER] {primitive.transform.name}";

            //In order to get the collider to work with cedmod
            collider.gameObject.layer = (primitive.MaterialColor.a < 1 ? LayerMask.NameToLayer("Glass") : 0);

            MeshCollider meshCollider = collider.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = PrimitiveObjectToy.PrimitiveTypeToMesh[primitive.PrimitiveType];
            serverSideColliders.Add(meshCollider);
          }

          gameObjectToDestroy.Add(primitive.gameObject);
        }
      }

      Dictionary<LightSourceToy, bool> lightToOptimize = GetToOptimize<LightSourceToy>(ev.Schematic.transform, parentsToExlude);
      if (lightToOptimize != null && !lightToOptimize.IsEmpty())
      {
        foreach (LightSourceToy light in lightToOptimize.Keys)
        {
          clientSideElement.Add(new ClientSideLight(light), lightToOptimize[light]);
          gameObjectToDestroy.Add(light.gameObject);
        }
      }

      Dictionary<DoorVariant, bool> doorsToOptimize = GetToOptimize<DoorVariant>(ev.Schematic.transform, parentsToExlude);
      if (doorsToOptimize != null && !doorsToOptimize.IsEmpty())
      {
        foreach (DoorVariant door in doorsToOptimize.Keys)
        {
          clientSideElement.Add(new ClientSideDoor(door, door.GetDoorType()), doorsToOptimize[door]);
          gameObjectToDestroy.Add(door.gameObject);
        }
      }

      float distanceForClusterSpawn = distanceRequiredForUnspawning;
      if (CustomSchematicSpawnDistance.TryGetValue(ev.Schematic.Name, out float customDistance))
      {
        distanceForClusterSpawn = customDistance;
      }

      OptimizedSchematic schematic = new OptimizedSchematic(ev.Schematic, serverSideColliders, clientSideElement,
        hideDistantPrimitives, distanceForClusterSpawn, excludedNamesForUnspawningDistantObjects,
        maxDistanceForPrimitiveCluster, maxPrimitivesPerCluster);

      optimizedSchematics.Add(schematic);

      foreach (GameObject gameObject in gameObjectToDestroy)
      {
        GameObject.Destroy(gameObject);
      }

      schematic.schematicServerSideElmentCount = 0;
      schematic.schematicServerEmptiesElementSideCount = 0;
      Timing.CallDelayed(1f, () =>
      {
        if (ev.Schematic == null || schematic == null) return;
        schematic.schematicServerSideElmentCount += ev.Schematic.GetComponentsInChildren<LightSourceToy>().Count(p => p != null);
        schematic.schematicServerSideElmentCount += ev.Schematic.GetComponentsInChildren<PrimitiveObjectToy>().Count(p => p != null);
        schematic.schematicServerEmptiesElementSideCount += ev.Schematic.GetComponentsInChildren<PrimitiveObjectToy>().Count(p => p != null && p.PrimitiveFlags == PrimitiveFlags.None);
      });
    }

    private void OnSchematicDestroyed(SchematicDestroyedEventArgs ev)
    {
      foreach (OptimizedSchematic optimizedSchematic in optimizedSchematics.Where(s => s != null).ToList())
      {
        if (optimizedSchematic.schematic == null || optimizedSchematic.schematic == ev.Schematic)
        {
          optimizedSchematic.Destroy();
          optimizedSchematics.Remove(optimizedSchematic);
        }
      }
    }

  }
}
