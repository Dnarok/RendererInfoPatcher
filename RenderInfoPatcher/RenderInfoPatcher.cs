using BepInEx;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using UnityEngine;
using R2API.Utils;
using System;
using System.Xml.Linq;
using UnityEngine.AddressableAssets;

namespace RenderInfoPatcher
{
    public static class Extension
    {
        public static bool MatchAny(this Instruction instruction, out Instruction param)
        {
            param = instruction;
            return true;
        }
    }

    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "Dnarok";
        public const string PluginName = "RenderInfoPatcher";
        public const string PluginVersion = "1.0.0";

        public void Awake()
        {
            Log.Init(Logger);

            // The only usage of RenderInfo that actually uses the default
            // material address is for skins - we're going to make it work for
            // the other cases, too.

            // Item displays is how I first ran into this issue - naturally,
            // they don't use their own new solution.
            IL.RoR2.ItemDisplay.RefreshRenderers += (il) =>
            {
                var cursor = new ILCursor(il);
                int location = 0;
                if (!cursor.TryGotoNext
                    (
                        x => x.MatchLdloc(out location),
                        x => x.MatchLdfld(typeof(RoR2.CharacterModel.RendererInfo), nameof(RoR2.CharacterModel.RendererInfo.renderer)),
                        x => x.MatchLdloc(out _),
                        x => x.MatchLdfld(typeof(RoR2.CharacterModel.RendererInfo), nameof(RoR2.CharacterModel.RendererInfo.defaultMaterial)),
                        x => x.MatchCallOrCallvirt(typeof(Renderer), "set_" + nameof(Renderer.material))
                    )
                )
                {
                    Log.Error("Couldn't patch ItemDisplay.RefreshRenderers.");
                }

                var label = cursor.DefineLabel();
                cursor.Emit(OpCodes.Br, label);
                cursor.GotoNext(MoveType.After, x => x.MatchCallOrCallvirt(typeof(Renderer), "set_" + nameof(Renderer.material)));
                cursor.MarkLabel(label);

                cursor.Emit(OpCodes.Ldloc, location);
                cursor.EmitDelegate(RefreshRenderersCorrection);
            };

            // It should probably also use the appropriate material when it is
            // updating, huh?
            IL.RoR2.CharacterModel.UpdateMaterials += (il) =>
            {
                var cursor = new ILCursor(il);
                if (!cursor.TryGotoNext
                    (
                        x => x.MatchLdfld(typeof(RoR2.CharacterModel.RendererInfo), nameof(RoR2.CharacterModel.RendererInfo.defaultMaterial)),
                        x => x.MatchLdarg(0)
                    )
                )
                {
                    Log.Error("Couldn't patch CharacterModel.UpdateMaterials.");
                }

                // var label = cursor.DefineLabel();
                // cursor.Emit(OpCodes.Br, label);
                // cursor.GotoNext(x => x.MatchLdarg(0));
                // cursor.MarkLabel(label);

                // balancing the stack before a branch is too much brain power
                // for me, we're just gonna smite the motherfucker.
                cursor.Remove();

                cursor.EmitDelegate(UpdateMaterialsCorrection);
            };

            // This seems like somewhere the material gets instantiated, so we
            // will have to play along by loading the material. Maybe the setter
            // here will make the rest of these unnecessary? Who knows?
            IL.RoR2.RandomizeSplatBias.Setup += (il) =>
            {
                var cursor = new ILCursor(il);
                if (!cursor.TryGotoNext
                    (
                        x => x.MatchLdfld(typeof(RoR2.CharacterModel.RendererInfo), nameof(RoR2.CharacterModel.RendererInfo.defaultMaterial)),
                        x => x.MatchCallOrCallvirt(typeof(UnityEngine.Object), nameof(Instantiate))
                    )
                )
                {
                    Log.Error("Couldn't patch RandomizeSplatBias.Setup.");
                }

                // var label = cursor.DefineLabel();
                // cursor.Emit(OpCodes.Br, label);
                // cursor.GotoNext(x => x.MatchCallOrCallvirt(typeof(UnityEngine.Object), nameof(Instantiate)));
                // cursor.MarkLabel(label);

                // balancing the stack before a branch is too much brain power
                // for me, we're just gonna smite the motherfucker.
                cursor.Remove();

                cursor.EmitDelegate(SetupCorrection);
            };

            // The fucking equality comparison didn't implement it. Why is this
            // so half-assed?
            On.RoR2.CharacterModel.RendererInfo.Equals += (orig, ref self, other) =>
            {
                return self.defaultMaterialAddress.Equals(other.defaultMaterialAddress) && orig(ref self, other);
            };
        }

        public static void RefreshRenderersCorrection(RoR2.CharacterModel.RendererInfo info)
        {
            if (info.defaultMaterialAddress != null && info.defaultMaterialAddress.RuntimeKeyIsValid())
            {
                info.renderer.material = Addressables.LoadAssetAsync<Material>(info.defaultMaterialAddress).WaitForCompletion();
            }
            else
            {
                info.renderer.material = info.defaultMaterial;
            }
        }
        public static Material UpdateMaterialsCorrection(ref RoR2.CharacterModel.RendererInfo info)
        {
            if (info.defaultMaterialAddress != null && info.defaultMaterialAddress.RuntimeKeyIsValid())
            {
                return Addressables.LoadAssetAsync<Material>(info.defaultMaterialAddress).WaitForCompletion();
            }
            else
            {
                return info.defaultMaterial;
            }
        }
        public static Material SetupCorrection(RoR2.CharacterModel.RendererInfo info)
        {
            if (info.defaultMaterialAddress != null && info.defaultMaterialAddress.RuntimeKeyIsValid())
            {
                return Addressables.LoadAssetAsync<Material>(info.defaultMaterialAddress).WaitForCompletion();
            }
            else
            {
                return info.defaultMaterial;
            }
        }
    }
}