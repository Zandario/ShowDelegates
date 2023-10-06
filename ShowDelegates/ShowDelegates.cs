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
        public override string Author => "art0007i & Zandario";
        public override string Version => "2.3.2";
        public override string Link => "https://github.com/art0007i/ShowDelegates/";

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> KEY_DEFAULT_OPEN =
            new("default_open", "If true delegates will be expanded by default.", () => false);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> KEY_SHOW_DELEGATES =
            new("show_delegates", "If false delegates will not be shown.", () => true);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> KEY_SHORT_NAMES =
            new("short_names", "Show short delegate names.", () => true);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> KEY_SHOW_NON_DEFAULT =
            new(
                "show_non_default",
                "If false only delegates that appear in vanilla will be shown.",
                () => true
            );

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> KEY_SHOW_HIDDEN =
            new(
                "show_hidden",
                "If true items hidden with the HideInInspector attribute will be shown",
                () => true
            );

        private static ModConfiguration config;

        public override void OnEngineInit()
        {
            config = GetConfiguration();
            Harmony harmony = new("me.art0007i.ShowDelegates");
            harmony.PatchAll();
        }

        #region Internal Functions

        private static string ToFancyEntry(MethodInfo info)
        {
            return string.Concat(
                new string[]
                {
                    info.IsStatic ? "Static " : "",
                    " ",
                    info.ToString()
                        .Substring(info.ToString().IndexOf(" "))
                        .Replace("FrooxEngine.", "")
                }
            );
        }

        private static void GenerateProxy<T>(UIBuilder ui, T target, DelegateInfo info)
            where T : class
        {
            Slot buttonSlot;
            // Display the button.
            // bool returnsVoid = info.ReturnType == typeof(void);
            if (!config.GetValue(KEY_SHORT_NAMES))
            {
                buttonSlot = ui.Button(
                    (LocaleString)(
                        // Add "Static " if the delegate is static.
                        ToFancyEntry(info.Method) ?? "No Delegates? 👽"
                    )
                ).Slot;
            }
            else
            {
                string returnType = info.NiceReturnType;
                if (returnType.StartsWith("Task<") && returnType.EndsWith(">"))
                {
                    returnType = returnType.Substring(5, returnType.Length - 6).BeautifyName();
                }
                buttonSlot = ui.HorizontalElementWithLabel(
                    (LocaleString)returnType,
                    0.2f,
                    () =>
                        ui.Button(
                            // Display the button's label.
                            (LocaleString)(
                                // Add "Static " if the delegate is static.
                                info.Static
                                    ? "Static "
                                    : "" + info.Name.BeautifyName() ?? "No Delegates? 👽"
                            ),
                            new colorX?(RadiantUI_Constants.BUTTON_COLOR)
                        ),
                    0.01f
                ).Slot;
            }

            switch (target)
            {
                case Delegate method:
                {
                    DelegateProxySource<Delegate> delegateProxySource = buttonSlot.AttachComponent<
                        DelegateProxySource<Delegate>
                    >();
                    delegateProxySource.Delegate.Target = method;
                    break;
                }

                case IWorldElement worldElement:
                {
                    ReferenceProxySource referenceProxySource =
                        buttonSlot.AttachComponent<ReferenceProxySource>();
                    referenceProxySource.Reference.Target = worldElement;
                    break;
                }
            }
        }

        #endregion

        #region Patch

        [HarmonyPatch(
            typeof(WorkerInitializer),
            nameof(WorkerInitializer.Initialize),
            new Type[] { typeof(Type) }
        )]
        public static class InitializeAllDelegatesPatch
        {
            public static IEnumerable<CodeInstruction> Transpiler(
                IEnumerable<CodeInstruction> instructions
            )
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
            public static void Prefix(
                IButton button,
                ButtonEventData eventData,
                ref Delegate target
            )
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

            private static bool Prefix(
                WorkerInspector __instance,
                Worker worker,
                UIBuilder ui,
                Predicate<ISyncMember> memberFilter = null
            )
            {
                List<ISyncMember> hidden = Pool.BorrowList<ISyncMember>();
                for (int i = 0; i < worker.SyncMemberCount; i++)
                {
                    ISyncMember syncMember = worker.GetSyncMember(i);

                    if (memberFilter != null && !memberFilter(syncMember))
                    {
                        continue;
                    }
                    if (
                        worker
                            .GetSyncMemberFieldInfo(i)
                            .GetCustomAttribute<HideInInspectorAttribute>() == null
                    )
                    {
                        SyncMemberEditorBuilder.Build(
                            syncMember,
                            worker.GetSyncMemberName(i),
                            worker.GetSyncMemberFieldInfo(i),
                            ui
                        );
                    }
                    else
                    {
                        hidden.Add(syncMember);
                    }
                }

                // Me disabling this is my way of saying I don't know what I'm doing. @Zandario
                // if (config.GetValue(KEY_SHOW_HIDDEN))
                // {
                //     foreach (ISyncMember item in hidden.Where(item => item != null))
                //     {
                //         GenerateProxy(ui, item, new DelegateInfo(item));
                //     }
                // }

                if (!config.GetValue(KEY_SHOW_DELEGATES))
                {
                    return false;
                }

                if (worker.SyncMethodCount > 0)
                {
                    WorkerInitInfo initInfo = Traverse
                        .Create(worker)
                        .Field<WorkerInitInfo>("InitInfo")
                        .Value;
                    IEnumerable<SyncMethodInfo> syncFuncs = config.GetValue(KEY_SHOW_NON_DEFAULT)
                        ? initInfo.syncMethods.AsEnumerable()
                        : initInfo.syncMethods.Where((m) => m.methodType != typeof(Delegate));

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

                    RadiantUI_Constants.SetupDefaultStyle(ui);
                    VerticalLayout root = ui.VerticalLayout(4f);
                    root.Slot.Name = "Delegates";
                    _ = root.Slot.RemoveComponent(root.Slot.GetComponent<LayoutElement>());

                    RectTransform header;
                    RectTransform content;
                    ui.VerticalHeader(58f, out header, out content);
                    ui.NestInto(header);

                    // The header button that opens the list
                    Button button = ui.Button(
                        "<b>Delegates</b>",
                        new colorX?(RadiantUI_Constants.BUTTON_COLOR)
                    );
                    button.Label.Color.Value = RadiantUI_Constants.LABEL_COLOR;

                    // Expander component that hooks up the button to the section
                    Expander expander = button.Slot.AttachComponent<Expander>();
                    expander.SectionRoot.Target = content.Slot;
                    expander.IsExpanded = config.GetValue(KEY_DEFAULT_OPEN);
                    ui.NestOut();

                    // Content Section
                    ui.NestInto(content);
                    ui.LayoutTarget = content.Slot;
                    ui.VerticalLayout(4f);
                    content.Slot.ActiveSelf = false;
                    _ = content.Slot.RemoveComponent(content.Slot.GetComponent<LayoutElement>());

                    foreach (SyncMethodInfo info in syncFuncs)
                    {
                        // Get the type of the delegate
                        Type delegateType = info.methodType;
                        DelegateInfo delegateInfo = new(info.method);

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
                                Error(
                                    $"Critical miss. Please report this message to the mod author: Could not identify {info.method} on type {info.method.DeclaringType}"
                                );
                                _ = ui.Text(
                                    $"<color=orange>{delegateInfo.Name}</color>",
                                    true,
                                    Alignment.MiddleLeft
                                );
                                continue;
                            }
                        }

                        // Generate a delegate and a proxy for the delegate
                        Delegate method = info.method.IsStatic
                            ? info.method.CreateDelegate(delegateType)
                            : info.method.CreateDelegate(delegateType, worker);
                        // Get the name of the delegate
                        // string[] delegateStrings = GenerateDelegateName(delegateType.ToString(), info.method);

                        // Generate the delegate proxy
                        GenerateProxy(ui, method, delegateInfo);
                    }
                    ui.NestOut();
                    ui.NestOut();
                    // ui.NestOut();
                }
                return false;
            }
        }

        #endregion
    }
}
