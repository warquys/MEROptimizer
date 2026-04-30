using System.Collections.Generic;
using System.Linq;
using AdminToys;
using LabApi.Features.Wrappers;
using UnityEngine;

namespace MEROptimizer.Application.Components
{
  public class ElementCluster : MonoBehaviour
  {
    public int id { get; set; }
    public List<IClientSideElement> elements { get; set; }

    public ClientSidePrimitive displayClusterPrimitive { get; set; }

    public Dictionary<Player, List<IClientSideElement>> awaitingSpawn = new Dictionary<Player, List<IClientSideElement>>();
    public List<Player> endSpawning = new List<Player>();

    public HashSet<Player> insidePlayers = new HashSet<Player>();

    public bool instantSpawn;

    private float numberOfPrimitivePerSpawn;

    private int updatePassed = 0;

    private bool multiFrameSpawn = false;

    public bool spawning = false;

    public void Start()
    {
      instantSpawn = MEROptimizer.numberOfPrimitivePerSpawn == 0;

      if (MEROptimizer.numberOfPrimitivePerSpawn < 1 && MEROptimizer.numberOfPrimitivePerSpawn > 0)
      {
        numberOfPrimitivePerSpawn = MEROptimizer.numberOfPrimitivePerSpawn * 10;
        multiFrameSpawn = true;
      }
      else
      {
        numberOfPrimitivePerSpawn = MEROptimizer.numberOfPrimitivePerSpawn;
      }

      float radius = this.GetComponent<SphereCollider>().radius;
      displayClusterPrimitive = new ClientSidePrimitive(this.transform.position - new Vector3(0, 2000, 0),
        this.transform.rotation, Vector3.one * (radius), PrimitiveType.Sphere, new Color(1, 0, 1, .4f), PrimitiveFlags.Visible);
    }

    public void OnDestroy()
    {
      foreach (ClientSidePrimitive primitive in elements)
      {
        primitive.DestroyForEveryone();
      }
      displayClusterPrimitive?.DestroyForEveryone();
    }

    public void OnTriggerEnter(Collider collider)
    {
      // check player, collider.CompareTag("Player") blabla
      if (collider == null || collider.transform.parent != null) return;

      if (!collider.CompareTag("Player") || !collider.gameObject.TryGetComponent(out PlayerTrigger playerTrigger)) return;

      // Prevents desync (using commands or mirrors skill issue), dosn't seems to happen without using dp commands
      // UnspawnFor(player);

      Player player = playerTrigger.player;

      if (player == null) return;

      if (!Application.MEROptimizer.ShouldTutorialsBeAffectedByDistanceSpawning && player.Role == PlayerRoles.RoleTypeId.Tutorial) return;

      if (player.Role == PlayerRoles.RoleTypeId.Filmmaker) return;

      AddPlayer(player);
    }

    public void AddPlayer(Player player)
    {
      if (!player.IsNpc)
      {
        if (instantSpawn)
        {
          SpawnFor(player);
        }
        else
        {
          awaitingSpawn.Remove(player);
          awaitingSpawn.Add(player, elements.ToList());
          spawning = true;
        }

      }

      insidePlayers.Add(player);
    }

    public void RemovePlayer(Player player)
    {
      awaitingSpawn.Remove(player);
      UnspawnFor(player);
      insidePlayers.Remove(player);
    }

    public void Update()
    {
      if (!spawning) return;

      if (multiFrameSpawn)
      {
        updatePassed++;
        if (updatePassed < numberOfPrimitivePerSpawn) return;
        updatePassed = 0;

      }

      if (awaitingSpawn.Count == 0)
      {
        spawning = false;
      }

      foreach (KeyValuePair<Player, List<IClientSideElement>> player_list in awaitingSpawn)
      {
        Player player = player_list.Key;
        List<IClientSideElement> list = player_list.Value;

        for (int i = 0; list.Count > 0 && i < (multiFrameSpawn ? 1 : numberOfPrimitivePerSpawn); i++)
        {
          IClientSideElement prim = list[0];

          list.Remove(prim);

          prim.SpawnClient(player);

          foreach (Player p in player.CurrentSpectators)
          {
            prim.SpawnClient(p);
          }
        }

        if (list.IsEmpty())
        {
          endSpawning.Add(player);
        }
      }

      foreach (Player player in endSpawning)
      {
        awaitingSpawn.Remove(player);
      }

      endSpawning.Clear();
    }
    public void OnTriggerExit(Collider collider)
      {
      if (collider == null || collider.transform.parent != null) return;

      if (!collider.CompareTag("Player") || !collider.gameObject.TryGetComponent(out PlayerTrigger playerTrigger)) return;

      // Prevents desync (using commands or mirrors skill issue), dosn't seems to happen without using dp commands
      // UnspawnFor(player);

      Player player = playerTrigger.player;

      if (player == null) return;

      if (!Application.MEROptimizer.ShouldTutorialsBeAffectedByDistanceSpawning && player.Role == PlayerRoles.RoleTypeId.Tutorial) return;

      if (player.Role == PlayerRoles.RoleTypeId.Filmmaker) return;
      
      RemovePlayer(player);
    }

    public void SpawnFor(Player player)
    {
      if (player == null || player.IsNpc) return;
      foreach (IClientSideElement element in elements)
      {
        element.SpawnClient(player);
      }
    }

    public void UnspawnFor(Player player)
    {
      if (player == null || player.IsNpc) return;

      awaitingSpawn.Remove(player);

      List<Player> spectatingPlayers = player.CurrentSpectators;
      foreach (IClientSideElement element in elements)
      {
        element.DestroyClient(player);

        foreach (Player p in spectatingPlayers)
        {
          element.DestroyClient(p);
        }
      }
    }

    public void DisplayRadius(Player player)
    {
      displayClusterPrimitive?.SpawnClient(player);
    }

    public void HideRadius(Player player)
    {
      displayClusterPrimitive?.DestroyClient(player);
    }
  }
}
