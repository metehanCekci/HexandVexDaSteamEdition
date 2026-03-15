using UnityEngine;
using System.Collections;

public class MapManager : MonoBehaviour
{
    public static MapManager instance;

    [Header("Layer Config")]
    public MapLayerData[] layerConfigs; // Inspector'dan ata — şimdilik tek layer yeterli

    [Header("Map UI")]
    public MapUI mapUI;

    [Header("Runtime State")]
    public MapData currentMap;

    private bool isTransitioning;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ─── Yeni oyun başlarken çağır ───
    public void StartNewRun()
    {
        GenerateNewMap(0);
        ShowMap();
    }

    // ─── Yeni layer haritası üret ───
    public void GenerateNewMap(int layerIndex)
    {
        MapLayerData config = GetLayerConfig(layerIndex);
        currentMap = MapGenerator.Generate(config, layerIndex);

        // İlk node'u (row 0) otomatik olarak "current" yap ama visited yapma
        // Oyuncu ilk combat'a girmeden map'i görecek
        if (currentMap.nodes.Count > 0)
        {
            // Start node'u bul (row 0)
            foreach (var node in currentMap.nodes)
            {
                if (node.row == 0)
                {
                    currentMap.currentNodeId = -1; // Henüz hiçbir node'a girilmedi
                    break;
                }
            }
        }

        if (mapUI != null) mapUI.BuildMap(currentMap);
    }

    // ─── Oyuncu bir node seçtiğinde ───
    public void SelectNode(int nodeId)
    {
        if (isTransitioning) return;
        if (currentMap == null) return;

        MapNode node = currentMap.GetNode(nodeId);
        if (node == null) return;

        // İlk hamle: row 0 node'larından birine girebilir
        if (currentMap.currentNodeId == -1)
        {
            if (node.row != 0) return;
        }
        else
        {
            // Sadece current node'un child'larına gidilebilir
            MapNode current = currentMap.GetNode(currentMap.currentNodeId);
            if (current == null || !current.childIds.Contains(nodeId)) return;
        }

        node.visited = true;
        currentMap.currentNodeId = nodeId;

        if (mapUI != null) mapUI.RefreshNodeStates(currentMap);

        // Node tipine göre dispatch
        ExecuteNode(node);
    }

    // ─── Node tipine göre aksiyon başlat ───
    private void ExecuteNode(MapNode node)
    {
        isTransitioning = true;

        // RunManager'a aktif node tipini bildir
        if (RunManager.instance != null)
            RunManager.instance.currentNodeType = node.nodeType;

        switch (node.nodeType)
        {
            case MapNodeType.Combat:
            case MapNodeType.EliteCombat:
                HideMap();
                RunManager.instance.currentLevel++;
                if (ScreenFader.instance != null)
                {
                    ScreenFader.instance.FadeAndLoad(() =>
                    {
                        LevelGenerator.instance.GenerateNextLevel();
                    });
                }
                else
                {
                    LevelGenerator.instance.GenerateNextLevel();
                }
                isTransitioning = false;
                break;

            case MapNodeType.Shop:
                HideMap();
                if (Shopmanager.instance != null)
                {
                    Shopmanager.instance.OpenAsMapNode();
                }
                isTransitioning = false;
                break;

            case MapNodeType.PerkSelection:
                HideMap();
                if (LevelUpManager.instance != null)
                {
                    LevelUpManager.instance.ShowLevelUpScreen();
                }
                isTransitioning = false;
                break;

            case MapNodeType.Rest:
                HideMap();
                if (RestNodeUI.instance != null)
                {
                    RestNodeUI.instance.Show();
                }
                isTransitioning = false;
                break;

            case MapNodeType.Event:
                // Stub: şimdilik combat gibi davransın
                HideMap();
                RunManager.instance.currentLevel++;
                if (ScreenFader.instance != null)
                {
                    ScreenFader.instance.FadeAndLoad(() =>
                    {
                        LevelGenerator.instance.GenerateNextLevel();
                    });
                }
                else
                {
                    LevelGenerator.instance.GenerateNextLevel();
                }
                isTransitioning = false;
                break;

            case MapNodeType.Boss:
                HideMap();
                RunManager.instance.currentLevel++;
                if (ScreenFader.instance != null)
                {
                    ScreenFader.instance.FadeAndLoad(() =>
                    {
                        LevelGenerator.instance.GenerateBossArena();
                    });
                }
                else
                {
                    LevelGenerator.instance.GenerateBossArena();
                }
                isTransitioning = false;
                break;
        }
    }

    // ─── Combat/Shop/Perk/Rest bittikten sonra haritaya dön ───
    public void OnNodeComplete()
    {
        isTransitioning = false;

        if (mapUI != null) mapUI.RefreshNodeStates(currentMap);

        // Boss node ise layer'ı ilerlet
        MapNode current = currentMap?.GetNode(currentMap.currentNodeId);
        if (current != null && current.nodeType == MapNodeType.Boss)
        {
            OnBossDefeated();
            return;
        }

        ShowMap();
    }

    // ─── Boss yenildiğinde yeni layer'a geç ───
    public void OnBossDefeated()
    {
        isTransitioning = false;

        if (RunManager.instance != null)
        {
            RunManager.instance.currentLayerIndex++;

            // Boss legendary multiplier'ı burada artır (LevelGenerator'dan taşındı)
            // LevelGenerator.GenerateNextLevel() zaten bunu kontrol ediyor ama
            // ileride tamamen buraya taşınacak
        }

        // Yeni layer haritası üret
        int newLayerIndex = RunManager.instance != null ? RunManager.instance.currentLayerIndex : 0;
        GenerateNewMap(newLayerIndex);
        ShowMap();
    }

    // ─── Map UI göster/gizle ───
    public void ShowMap()
    {
        if (mapUI != null)
        {
            mapUI.RefreshNodeStates(currentMap);
            mapUI.Show();
        }
    }

    public void HideMap()
    {
        if (mapUI != null) mapUI.Hide();
    }

    // ─── Layer config'i güvenli al (fallback: son config) ───
    private MapLayerData GetLayerConfig(int layerIndex)
    {
        if (layerConfigs == null || layerConfigs.Length == 0)
        {
            Debug.LogError("MapManager: layerConfigs boş! Default MapLayerData kullanılıyor.");
            return ScriptableObject.CreateInstance<MapLayerData>();
        }

        int idx = Mathf.Clamp(layerIndex, 0, layerConfigs.Length - 1);
        return layerConfigs[idx];
    }
}
