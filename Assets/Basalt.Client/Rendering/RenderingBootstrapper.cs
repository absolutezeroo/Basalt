using System;
using System.Collections.Generic;
using System.IO;
using Basalt.Core;
using UnityEngine;

namespace Basalt.Client
{
    /// <summary>
    /// Orchestrates the rendering startup sequence: texture loading, node registration,
    /// material creation, and injection into the chunk management system.
    /// </summary>
    /// <remarks>
    /// Startup order:
    /// 1. Create <see cref="TextureRegistry"/>
    /// 2. Build <see cref="Texture2DArray"/> from mod textures
    /// 3. Set texture resolver on <see cref="NodeRegistry"/>
    /// 4. Register node definitions (hardcoded for now; future: Lua scripting)
    /// 5. Bake registry into NativeArrays
    /// 6. Create <see cref="VoxelMaterial"/>
    /// </remarks>
    public class RenderingBootstrapper : MonoBehaviour
    {
        [Header("Texture Settings")]
        [SerializeField] private int _textureSize = 16;

        [Header("References")]
        [SerializeField] private ChunkManager _chunkManager;

        private NodeRegistry _nodeRegistry;
        private TextureRegistry _textureRegistry;
        private VoxelMaterial _voxelMaterial;

        /// <summary>Gets the shared voxel material for chunk renderers.</summary>
        public Material VoxelMaterial => _voxelMaterial?.Material;

        /// <summary>Gets the node registry with baked definitions.</summary>
        public NodeRegistry NodeRegistry => _nodeRegistry;

        /// <summary>Gets the texture registry with resolved indices.</summary>
        public TextureRegistry TextureRegistry => _textureRegistry;

        private void Awake()
        {
            _nodeRegistry = new NodeRegistry();
            _textureRegistry = new TextureRegistry();

            // Build texture array from all mod directories
            string[] modPaths = DiscoverModPaths();
            TextureArrayBuilder.Build(modPaths, _textureRegistry, _textureSize);

            // Wire texture resolver into the node registry
            _nodeRegistry.SetTextureResolver(_textureRegistry);

            // Register node definitions (hardcoded for now — Epic 5 adds Lua)
            RegisterDefaultNodes();

            // Bake registry for Burst job access
            _nodeRegistry.Bake();

            // Create material with the texture array
            _voxelMaterial = new VoxelMaterial();
            _voxelMaterial.Initialize(_textureRegistry.TextureArray);

            Debug.Log(
                $"[Basalt] Rendering initialized: {_textureRegistry.Count} textures, " +
                $"{_nodeRegistry.RegisteredCount} nodes.");
        }

        /// <summary>
        /// Discovers mod directories by scanning the mods/ folder at the project root.
        /// </summary>
        private string[] DiscoverModPaths()
        {
            string modsRoot = Path.Combine(Application.dataPath, "..", "mods");
            var paths = new List<string>();

            if (Directory.Exists(modsRoot))
            {
                foreach (string dir in Directory.GetDirectories(modsRoot))
                {
                    paths.Add(dir);
                }
            }
            else
            {
                Debug.LogWarning("[Basalt] No mods/ directory found. No textures loaded.");
            }

            return paths.ToArray();
        }

        /// <summary>
        /// Registers hardcoded node definitions for testing. Will be replaced by Lua
        /// scripting in Epic 5.
        /// </summary>
        private void RegisterDefaultNodes()
        {
            RegisterNode("default:stone", "default_stone.png", DrawType.Normal);
            RegisterNode("default:dirt", "default_dirt.png", DrawType.Normal);
            RegisterNode("default:cobble", "default_cobble.png", DrawType.Normal);
            RegisterNode("default:sand", "default_sand.png", DrawType.Normal);
            RegisterNode("default:gravel", "default_gravel.png", DrawType.Normal);
            RegisterNode("default:clay", "default_clay.png", DrawType.Normal);
            RegisterNode("default:sandstone", "default_sandstone.png", DrawType.Normal);
            RegisterNode("default:obsidian", "default_obsidian.png", DrawType.Normal);
            RegisterNode("default:brick", "default_brick.png", DrawType.Normal);

            // TODO(Epic-5): replace with proper texture modifier evaluation once Lua scripting is live
            RegisterNodeMultiFace("default:dirt_with_grass",
                top: "default_grass.png",
                bottom: "default_dirt.png",
                sides: "default_grass_side.png",
                DrawType.Normal);

            RegisterNodeMultiFace("default:desert_sandstone",
                top: "default_desert_sandstone_top.png",
                bottom: "default_desert_sandstone_bottom.png",
                sides: "default_desert_sandstone.png",
                DrawType.Normal);
        }

        private void RegisterNode(string name, string texture, DrawType drawtype)
        {
            ushort tileIndex = _nodeRegistry.ResolveTexture(texture);

            _nodeRegistry.Register(name, new NodeDefinition
            {
                Drawtype = drawtype,
                Paramtype = ParamType.Light,
                TileTop = tileIndex,
                TileBottom = tileIndex,
                TileRight = tileIndex,
                TileLeft = tileIndex,
                TileFront = tileIndex,
                TileBack = tileIndex,
                Walkable = 1,
                Pointable = 1,
                Diggable = 1,
            });
        }

        private void RegisterNodeMultiFace(string name, string top, string bottom,
            string sides, DrawType drawtype)
        {
            ushort topIdx = _nodeRegistry.ResolveTexture(top);
            ushort bottomIdx = _nodeRegistry.ResolveTexture(bottom);
            ushort sideIdx = _nodeRegistry.ResolveTexture(sides);

            _nodeRegistry.Register(name, new NodeDefinition
            {
                Drawtype = drawtype,
                Paramtype = ParamType.Light,
                TileTop = topIdx,
                TileBottom = bottomIdx,
                TileRight = sideIdx,
                TileLeft = sideIdx,
                TileFront = sideIdx,
                TileBack = sideIdx,
                Walkable = 1,
                Pointable = 1,
                Diggable = 1,
            });
        }

        private void OnDestroy()
        {
            _voxelMaterial?.Dispose();
            _nodeRegistry?.Dispose();
        }
    }
}
