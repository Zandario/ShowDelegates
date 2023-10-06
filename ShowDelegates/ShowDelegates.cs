using HarmonyLib;
using ResoniteModLoader;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using FrooxEngine;
using FrooxEngine.UIX;
using FrooxEngine.ProtoFlux;
using Elements.Core;

namespace ShowDelegates
{
    public class ShowDelegates : ResoniteMod
    {
        public override string Name => "ShowDelegates";
        public override string Author => "art0007i";
        public override string Version => "2.2.2";
        public override string Link => "https://github.com/art0007i/ShowDelegates/";

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> KEY_DEFAULT_OPEN = new("default_open", "If true delegates will be expanded by default", () => false);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> KEY_SHOW_DELEGATES = new("show_deleages", "If false delegates will not be shown", () => true);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> KEY_SHORT_NAMES = new("short_names", "Show short delegate names.", () => true);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> KEY_SHOW_NON_DEFAULT = new("show_non_default", "If false only delegates that appear in vanilla will be shown.", () => true);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> KEY_SHOW_HIDDEN = new("show_hidden", "If true items hidden with the HideInInspector attribute will be shown", () => true);

        private static ModConfiguration config;

        public override void OnEngineInit()
        {
            config = GetConfiguration();
            Harmony harmony = new("me.art0007i.ShowDelegates");
            harmony.PatchAll();

        }

        #region Internal Functions
        private static void GenerateDelegateProxy<T>(UIBuilder ui, string name, T target) where T : class
        {
            // Push a new style scope to avoid affecting the style of the caller.
            ui.PushStyle();

            // Create a label.
            Text text = ui.Text((LocaleString)name, true, new Alignment?(Alignment.MiddleLeft), true, null);
            text.Slot.GetComponent<RectTransform>().AnchorMax.Value = new float2(0.25f, 1f);

            // Create a button.
            Button button = text.Slot.AttachComponent<Button>();

            // Create a color driver for the button.
            InteractionElement.ColorDriver colorDriver = button.ColorDrivers.Add();
            colorDriver.ColorDrive.Target = text.Color;
            RadiantUI_Constants.SetupLabelDriverColors(colorDriver);

            // Create a delegate proxy source.
            DelegateProxySource<T> delegateProxySource = text.Slot.AttachComponent<DelegateProxySource<T>>();

            // Configure the delegate proxy source.
            delegateProxySource.Delegate.Target = target;

            // Pop the style scope.
            ui.PopStyle();
        }

        private static void GenerateReferenceProxy(UIBuilder ui, string name, IWorldElement target)
        {
            // Push a new style scope to avoid affecting the style of the caller.
            ui.PushStyle();

            // Create a Text element with the specified name.
            LocaleString localeString = name + ":";
            Text text = ui.Text(localeString, true, new Alignment?(Alignment.MiddleLeft), true, null);
            text.Slot.GetComponent<RectTransform>().AnchorMax.Value = new float2(0.25f, 1f);

            // Create a Button element to serve as the clickable area.
            InteractionElement.ColorDriver colorDriver = text.Slot.AttachComponent<Button>().ColorDrivers.Add();
            colorDriver.ColorDrive.Target = text.Color;
            RadiantUI_Constants.SetupLabelDriverColors(colorDriver);

            // Create a ReferenceProxySource component that references the specified target.
            text.Slot.AttachComponent<ReferenceProxySource>().Reference.Target = target;

            // Create a referece proxy source.
            ReferenceProxySource referenceProxySource = text.Slot.AttachComponent<ReferenceProxySource>();

            // Configure the referece proxy source.
            referenceProxySource.Reference.Target = target;

            // Pop the style scope.
            ui.PopStyle();
        }

        private static string GenerateDelegateName(string prefix, MethodInfo info)
        {
            // if short names are enabled
            if (config.GetValue(KEY_SHORT_NAMES))
            {
                // get all parameters
                string parameters = string.Join(", ", info.GetParameters().Select(p => $"{p.ParameterType.GetNiceName()} {p.Name}"));
                // return a short name
                return $"{info.ReturnType.GetNiceName()} {info.Name}({parameters})";
            }

            // if the method is static
            if (info.IsStatic)
            {
                // include the "Static" prefix
                prefix = "Static " + prefix;
            }

            // get the full name of the method
            string fullName = info.ToString();
            // remove the namespace prefix
            fullName = fullName.Substring(fullName.IndexOf(" "));
            // remove the "FrooxEngine." prefix
            fullName = fullName.Replace("FrooxEngine.", "");

            // return the name with the prefix and return type
            return $"{prefix} {fullName} -> {info.ReturnType.Name}";
        }



        private static void GenerateUI(UIBuilder ui)
        {
            ui.PushStyle();

            // The top-level button that opens the section
            _ = ui.HorizontalLayout(4f, 0f, Alignment.MiddleLeft);
            RadiantUI_Constants.SetupDefaultStyle(ui);
            ui.Style.ButtonTextPadding *= 2f;
            ui.Style.MinHeight = ui.Style.MinWidth = 32f;
            Button button = ui.Button((LocaleString)"Delegates");
            ui.NestOut();

            // The section that contains the delegates
            VerticalLayout delegates = ui.VerticalLayout();
            delegates.Slot.ActiveSelf = false;
            _ = delegates.Slot.RemoveComponent(delegates.Slot.GetComponent<LayoutElement>());

            // Expander component that hooks up the button to the section
            Expander expander = button.Slot.AttachComponent<Expander>();
            expander.SectionRoot.Target = delegates.Slot;
            expander.IsExpanded = config.GetValue(KEY_DEFAULT_OPEN);

            ui.PopStyle();
        }

        void GenerateUIButBetter(UIBuilder ui)
        {
            ui.PushStyle();

            // The top-level button that opens the section
            _ = ui.HorizontalLayout(4f, 0f, Alignment.MiddleLeft);
            RadiantUI_Constants.SetupDefaultStyle(ui);
            ui.Style.ButtonTextPadding *= 2f;
            ui.Style.MinHeight = ui.Style.MinWidth = 32f;
            Button button = ui.Button((LocaleString)"<b>SyncMethods</b>");
            ui.NestOut();

            // The section that contains the delegates
            VerticalLayout delegates = ui.VerticalLayout();
            delegates.Slot.ActiveSelf = false;
            _ = delegates.Slot.RemoveComponent(delegates.Slot.GetComponent<LayoutElement>());

            // Expander component that hooks up the button to the section
            Expander expander = button.Slot.AttachComponent<Expander>();
            expander.SectionRoot.Target = delegates.Slot;
            expander.IsExpanded = config.GetValue(KEY_DEFAULT_OPEN);

            ui.PopStyle();
        }
        #endregion

        #region Patch

        [HarmonyPatch(typeof(WorkerInitializer), nameof(WorkerInitializer.Initialize), new Type[] { typeof(Type) })]
        public static class InitializeAllDelegatesPatch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> codes = instructions.ToList();
                for (int i = 0; i < codes.Count; i++)
                {
                    CodeInstruction code = codes[i];
                    if (code.operand is MethodInfo mf && mf.Name == nameof(Type.GetMethods))
                    {
                        codes[i - 1].operand = (sbyte)AccessTools.all;
                    }
                }
                return codes.AsEnumerable();
            }
        }

        [HarmonyPatch(typeof(ProtoFluxTool), "OnCreateDelegateProxy")]
        private class FixProtoFluxDeleagtes
        {
            public static void Prefix(IButton button, ButtonEventData eventData, ref Delegate target)
            {
                Type delegateType = Helper.GetFuncOrAction(target.Method);
                target = target.Method.CreateDelegate(delegateType, target.Target);
            }
        }

        [HarmonyPatch(typeof(WorkerInspector))]
        [HarmonyPatch("BuildInspectorUI")]
        private class WorkerInspector_BuildInspectorUI_Patch
        {
            // Static constructor to generate the lookup table.
            public static Dictionary<MethodArgs, Type> argumentLookup = new();

            private static bool Prefix(WorkerInspector __instance, Worker worker, UIBuilder ui, Predicate<ISyncMember> memberFilter = null)
            {
                List<ISyncMember> hidden = Pool.BorrowList<ISyncMember>();
                for (int i = 0; i < worker.SyncMemberCount; i++)
                {
                    ISyncMember syncMember = worker.GetSyncMember(i);

                    if (memberFilter != null && !memberFilter(syncMember))
                    {
                        continue;
                    }
                    if (worker.GetSyncMemberFieldInfo(i).GetCustomAttribute<HideInInspectorAttribute>() == null)
                    {
                        SyncMemberEditorBuilder.Build(syncMember, worker.GetSyncMemberName(i), worker.GetSyncMemberFieldInfo(i), ui);
                    }
                    else
                    {
                        hidden.Add(syncMember);
                    }
                }

                if (config.GetValue(KEY_SHOW_HIDDEN))
                {
                    foreach (ISyncMember item in hidden.Where(item => item != null))
                    {
                        GenerateReferenceProxy(ui, worker.GetSyncMemberName(item), item);
                    }
                }

                if (!config.GetValue(KEY_SHOW_DELEGATES))
                {
                    return false;
                }

                if (worker.SyncMethodCount > 0)
                {
                    WorkerInitInfo initInfo = Traverse.Create(worker).Field<WorkerInitInfo>("InitInfo").Value;
                    IEnumerable<SyncMethodInfo> syncFuncs = config.GetValue(KEY_SHOW_NON_DEFAULT) ? initInfo.syncMethods.AsEnumerable() : initInfo.syncMethods.Where((m) => m.methodType != typeof(Delegate));

                    if (!syncFuncs.Any())
                    {
                        return false;
                    }

                    // var myTxt = ui.Text("---- SYNC METHODS HERE ----", true, new Alignment?(Alignment.MiddleCenter), true, null);
                    // var delegates = ui.VerticalLayout();
                    // delegates.Slot.ActiveSelf = false;
                    // delegates.Slot.RemoveComponent(delegates.Slot.GetComponent<LayoutElement>());
                    // var expander = myTxt.Slot.AttachComponent<Expander>();
                    // expander.SectionRoot.Target = delegates.Slot;
                    // expander.IsExpanded = config.GetValue(KEY_DEFAULT_OPEN);
                    // var colorDriver = myTxt.Slot.AttachComponent<Button>().ColorDrivers.Add();
                    // colorDriver.ColorDrive.Target = myTxt.Color;
                    // RadiantUI_Constants.SetupLabelDriverColors(colorDriver);

                    GenerateUI(ui);

                    foreach (SyncMethodInfo info in syncFuncs)
                    {
                        // Get the type of the delegate
                        Type delegateType = info.methodType;

                        // If the method is not a delegate...
                        if (!typeof(MulticastDelegate).IsAssignableFrom(delegateType))
                        {
                            try
                            {
                                // Classify the delegate type. This could throw in many ways...
                                delegateType = Helper.ClassifyDelegate(info.method, argumentLookup);
                            }
                            catch (Exception e)
                            {
                                Error($"Error while classifying function {info.method}\n{e}");
                                delegateType = null;
                            }

                            if (delegateType == null)
                            {
                                Error($"Unmapped type. Please report this message to the mod author: Could not identify {info.method} on type {info.method.DeclaringType}");
                                _ = ui.Text($"<color=orange>{GenerateDelegateName("<i>unknown</i>", info.method)}</color>", true, Alignment.MiddleLeft);
                                continue;
                            }
                        }

                        // Create a delegate from the method.
                        Delegate method;
                        if (info.method.IsStatic)
                        {
                            method = info.method.CreateDelegate(delegateType);
                        }
                        else
                        {
                            method = info.method.CreateDelegate(delegateType, worker);
                        }

                        // Get the method to generate the delegate proxy
                        MethodInfo generateDelegateProxyMethod = typeof(ShowDelegates).GetMethod(nameof(GenerateDelegateProxy), BindingFlags.NonPublic | BindingFlags.Static);
                        // Get the name of the delegate
                        string delegateName = GenerateDelegateName(delegateType.ToString(), info.method);
                        // Generate the delegate proxy
                        object delegateProxy = generateDelegateProxyMethod.MakeGenericMethod(delegateType).Invoke(null, new object[] { ui, delegateName, method });
                    }
                    ui.NestOut();
                }
                return false;
            }

        }

        #endregion
    }
}
