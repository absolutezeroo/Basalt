using System;
using System.Collections.Generic;
using Unity.Collections;

namespace Basalt.Core
{
    /// <summary>
    /// Central registry for all node definitions.
    /// </summary>
    /// <remarks>
    /// Two layers:
    /// - Managed side: Dictionary&lt;string, ushort&gt; for name→id mapping (used by Lua/init)
    /// - Runtime side: NativeArray&lt;NodeDefinition&gt; indexed by content_id (used by Burst jobs)
    ///
    /// The NativeArray is "baked" after all registrations are complete, before the game loop starts.
    /// </remarks>
    public class NodeRegistry : IDisposable
    {
        private readonly Dictionary<string, ushort> _nameToId = new();
        private readonly List<string> _idToName = new();
        private readonly List<NodeDefinition> _definitions = new();
        private readonly List<GroupEntry> _groupEntries = new();
        private readonly Dictionary<string, int> _groupNameToId = new();

        private NativeArray<NodeDefinition> _runtimeDefs;
        private NativeArray<GroupEntry> _runtimeGroups;
        private ITextureResolver _textureResolver;
        private bool _baked;
        private int _nextGroupId;
        private ushort _nextContentId = 128;

        public NodeRegistry()
        {
            RegisterBuiltins();
        }

        /// <summary>
        /// Registers the three built-in node types that Luanti always has.
        /// </summary>
        private void RegisterBuiltins()
        {
            RegisterInternal("unknown", BasaltConstants.CONTENT_UNKNOWN, new NodeDefinition
            {
                Drawtype = DrawType.Normal,
                Paramtype = ParamType.None,
                Walkable = 1,
                Pointable = 1,
                Diggable = 1,
            });

            RegisterInternal("air", BasaltConstants.CONTENT_AIR, new NodeDefinition
            {
                Drawtype = DrawType.Airlike,
                Paramtype = ParamType.Light,
                SunlightPropagates = 1,
                BuildableTo = 1,
            });

            RegisterInternal("ignore", BasaltConstants.CONTENT_IGNORE, new NodeDefinition
            {
                Drawtype = DrawType.Airlike,
                Paramtype = ParamType.None,
                SunlightPropagates = 1,
            });
        }

        private void RegisterInternal(string name, ushort contentId, NodeDefinition def)
        {
            def.ContentId = contentId;

            while (_definitions.Count <= contentId)
            {
                _definitions.Add(default);
                _idToName.Add(string.Empty);
            }

            _definitions[contentId] = def;
            _idToName[contentId] = name;
            _nameToId[name] = contentId;
        }

        /// <summary>
        /// Registers a new node type. Returns the assigned content_id.
        /// Must be called before Bake(). Name format: "modname:nodename".
        /// </summary>
        /// <param name="name">Full node name in modname:nodename format.</param>
        /// <param name="def">The node definition properties.</param>
        /// <param name="groups">Optional group ratings (e.g. cracky=3).</param>
        /// <returns>The assigned content_id for this node.</returns>
        public ushort Register(string name, NodeDefinition def, Dictionary<string, int> groups = null)
        {
            if (_baked)
            {
                throw new InvalidOperationException("Cannot register nodes after Bake().");
            }

            if (_nameToId.ContainsKey(name))
            {
                throw new ArgumentException($"Node '{name}' is already registered.", nameof(name));
            }

            if (_nextContentId >= BasaltConstants.CONTENT_MAX)
            {
                throw new InvalidOperationException("Maximum node types reached.");
            }

            ushort id = _nextContentId++;
            def.ContentId = id;

            if (groups != null && groups.Count > 0)
            {
                def.GroupsOffset = _groupEntries.Count;
                def.GroupsCount = groups.Count;

                foreach (KeyValuePair<string, int> kvp in groups)
                {
                    int groupId = GetOrCreateGroupId(kvp.Key);
                    _groupEntries.Add(new GroupEntry { GroupId = groupId, Value = kvp.Value });
                }
            }

            RegisterInternal(name, id, def);

            return id;
        }

        /// <summary>
        /// Bakes the managed lists into NativeArrays for Burst job access.
        /// Call once after all nodes are registered, before the game loop.
        /// </summary>
        public void Bake()
        {
            if (_baked)
            {
                return;
            }

            _runtimeDefs = new NativeArray<NodeDefinition>(
                _definitions.Count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            // Luanti: nodedef.cpp — ContentFeatures::updateTextures() sets solidness
            for (int i = 0; i < _definitions.Count; i++)
            {
                NodeDefinition def = _definitions[i];
                def.Solidness = ComputeSolidness(def.Drawtype);
                def.VisualSolidness = ComputeVisualSolidness(def.Drawtype);
                // Resolve IsGroundContent tri-state into final 0/1:
                //   AUTO (0, default) → auto-compute from solidness/liquid
                //   TRUE  (1)         → keep as-is
                //   FALSE (2)         → user explicitly opted out, normalize to 0
                if (def.IsGroundContent == NodeDefinition.IS_GROUND_CONTENT_AUTO)
                {
                    def.IsGroundContent = (byte)((def.Solidness >= 2 && def.Liquid == LiquidType.None) ? 1 : 0);
                }
                else if (def.IsGroundContent == NodeDefinition.IS_GROUND_CONTENT_FALSE)
                {
                    def.IsGroundContent = 0;
                }
                _runtimeDefs[i] = def;
            }

            int groupCount = _groupEntries.Count > 0 ? _groupEntries.Count : 1;

            _runtimeGroups = new NativeArray<GroupEntry>(
                groupCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            for (int i = 0; i < _groupEntries.Count; i++)
            {
                _runtimeGroups[i] = _groupEntries[i];
            }

            _baked = true;
        }

        /// <summary>
        /// Sets the texture resolver used by <see cref="ResolveTexture"/> to map
        /// texture filenames to array slice indices. Call before node registration.
        /// </summary>
        /// <param name="resolver">The client-side texture resolver.</param>
        public void SetTextureResolver(ITextureResolver resolver)
        {
            _textureResolver = resolver;
        }

        /// <summary>
        /// Resolves a texture filename to its texture array slice index.
        /// Returns 0 (fallback) if no resolver is set or the name is not found.
        /// </summary>
        /// <param name="textureName">Texture filename (e.g. "default_stone.png").</param>
        /// <returns>The array slice index.</returns>
        public ushort ResolveTexture(string textureName)
        {
            if (_textureResolver == null)
            {
                return 0;
            }

            return _textureResolver.Resolve(textureName);
        }

        /// <summary>
        /// Gets the content_id for a node name, or CONTENT_UNKNOWN if not found.
        /// </summary>
        /// <param name="name">Full node name in modname:nodename format.</param>
        /// <returns>The content_id, or CONTENT_UNKNOWN if not registered.</returns>
        public ushort GetIdByName(string name)
        {
            return _nameToId.TryGetValue(name, out ushort id) ? id : BasaltConstants.CONTENT_UNKNOWN;
        }

        /// <summary>
        /// Gets the name for a content_id, or "unknown" if not found.
        /// </summary>
        /// <param name="contentId">The content_id to look up.</param>
        /// <returns>The registered node name.</returns>
        public string GetNameById(ushort contentId)
        {
            return contentId < _idToName.Count ? _idToName[contentId] : "unknown";
        }

        /// <summary>
        /// Checks whether a node name has been registered.
        /// </summary>
        public bool IsRegistered(string name) => _nameToId.ContainsKey(name);

        /// <summary>
        /// Gets the total number of registered node types (including builtins).
        /// </summary>
        public int RegisteredCount => _nextContentId - 128 + 3;

        /// <summary>
        /// Returns the baked NativeArray for read-only use in Burst jobs.
        /// </summary>
        public NativeArray<NodeDefinition>.ReadOnly RuntimeDefs
        {
            get
            {
                if (!_baked)
                {
                    throw new InvalidOperationException("Call Bake() before accessing RuntimeDefs.");
                }

                return _runtimeDefs.AsReadOnly();
            }
        }

        /// <summary>
        /// Returns the raw baked NativeArray for passing into jobs with [ReadOnly] attribute.
        /// </summary>
        /// <remarks>
        /// Use this when scheduling Burst jobs that need [ReadOnly] NativeArray fields.
        /// The returned array must NOT be written to or disposed by callers.
        /// </remarks>
        public NativeArray<NodeDefinition> RuntimeDefsNative
        {
            get
            {
                if (!_baked)
                {
                    throw new InvalidOperationException("Call Bake() before accessing RuntimeDefsNative.");
                }

                return _runtimeDefs;
            }
        }

        /// <summary>
        /// Returns the baked NativeArray of group entries for read-only use in Burst jobs.
        /// </summary>
        public NativeArray<GroupEntry>.ReadOnly RuntimeGroups
        {
            get
            {
                if (!_baked)
                {
                    throw new InvalidOperationException("Call Bake() before accessing RuntimeGroups.");
                }

                return _runtimeGroups.AsReadOnly();
            }
        }

        /// <summary>
        /// Gets the integer ID for a group name, or -1 if not found.
        /// </summary>
        public int GetGroupId(string groupName)
        {
            return _groupNameToId.TryGetValue(groupName, out int id) ? id : -1;
        }

        private int GetOrCreateGroupId(string groupName)
        {
            if (!_groupNameToId.TryGetValue(groupName, out int id))
            {
                id = _nextGroupId++;
                _groupNameToId[groupName] = id;
            }

            return id;
        }

        /// <summary>
        /// Computes the physical solidness value from a DrawType.
        /// Mirrors Luanti's solidness table in <c>luanti/src/client/node_visuals.cpp</c>.
        /// </summary>
        private static byte ComputeSolidness(DrawType drawtype)
        {
            return drawtype switch
            {
                DrawType.Normal => 2,
                DrawType.Liquid => 1,
                DrawType.PlantlikeRooted => 2,
                _ => 0,
            };
        }

        /// <summary>
        /// Computes the visual solidness value from a DrawType.
        /// Mirrors Luanti's visual_solidness table in <c>luanti/src/client/node_visuals.cpp</c>.
        /// </summary>
        private static byte ComputeVisualSolidness(DrawType drawtype)
        {
            return drawtype switch
            {
                DrawType.Normal => 1,
                DrawType.Glasslike => 1,
                DrawType.GlasslikeFramed => 1,
                DrawType.GlasslikeFramedOptional => 1,
                DrawType.Allfaces => 1,
                DrawType.PlantlikeRooted => 1,
                _ => 0,
            };
        }

        public void Dispose()
        {
            if (_runtimeDefs.IsCreated)
            {
                _runtimeDefs.Dispose();
            }

            if (_runtimeGroups.IsCreated)
            {
                _runtimeGroups.Dispose();
            }
        }
    }
}