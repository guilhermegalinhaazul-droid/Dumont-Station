// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Administration;
using Content.Server.DeviceLinking.Systems;
using Content.Server.Medical.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Forensics.Components;
using Content.Shared.Genetics;
using Content.Shared.Genetics.Systems;
using Content.Shared.Genetics.UI;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Power;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;

namespace Content.Server.Genetics.System
{
    [UsedImplicitly]
    public sealed partial class DnaModifierConsoleSystem : EntitySystem
    {
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly SharedContainerSystem _container = default!;
        [Dependency] private readonly DamageableSystem _damage = default!;
        [Dependency] private readonly DeviceLinkSystem _signalSystem = default!;
        [Dependency] private readonly DnaClientSystem _dnaClient = default!;
        [Dependency] private readonly DnaModifierSystem _dnaModifier = default!;
        [Dependency] private readonly IEntityManager _entManager = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly ItemSlotsSystem _itemSlotsSystem = default!;
        [Dependency] private readonly PowerReceiverSystem _powerReceiverSystem = default!;
        [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;
        [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly PopupSystem _popup = default!;

        private static readonly EntProtoId Injector = "DnaInjector";
        private static readonly ProtoId<DamageTypePrototype> RadDamage = "Radiation";

        public override void Initialize()
        {
            base.Initialize();
            InitializeSequencing();

            SubscribeLocalEvent<DnaModifierConsoleComponent, ComponentInit>(OnInit);
            SubscribeLocalEvent<DnaModifierConsoleComponent, AfterActivatableUIOpenEvent>(OnUIOpen);
            SubscribeLocalEvent<DnaModifierConsoleComponent, PowerChangedEvent>(OnPowerChanged);
            SubscribeLocalEvent<DnaModifierConsoleComponent, MapInitEvent>(OnMapInit);
            SubscribeLocalEvent<DnaModifierConsoleComponent, NewLinkEvent>(OnNewLink);
            SubscribeLocalEvent<DnaModifierConsoleComponent, PortDisconnectedEvent>(OnPortDisconnected);
            SubscribeLocalEvent<DnaModifierConsoleComponent, AnchorStateChangedEvent>(OnAnchorChanged);

            SubscribeLocalEvent<DnaModifierConsoleComponent, DnaModifierUpdateEvent>((uid, component, args) =>
            {
                if (GetEntity(args.Uid) == uid && _powerReceiverSystem.IsPowered(uid))
                    OnUpdateUI(args);
            });
            SubscribeLocalEvent<DnaModifierConsoleComponent, DnaModifierConsoleEjectEvent>((uid, component, args) =>
            {
                if (GetEntity(args.Uid) == uid && _powerReceiverSystem.IsPowered(uid))
                    OnEjectPressed(args);
            });
            SubscribeLocalEvent<DnaModifierConsoleComponent, DnaModifierConsoleEjectRejuveEvent>((uid, component, args) =>
            {
                if (GetEntity(args.Uid) == uid && _powerReceiverSystem.IsPowered(uid))
                    OnEjectRejuvePressed(args);
            });
            SubscribeLocalEvent<DnaModifierConsoleComponent, DnaModifierConsoleReagentButtonEvent>((uid, component, args) =>
            {
                if (GetEntity(args.Uid) == uid && _powerReceiverSystem.IsPowered(uid))
                    OnReagentButtonPressed(args);
            });

            SubscribeLocalEvent<DnaModifierConsoleComponent, DnaModifierConsoleSaveServerEvent>((uid, component, args) =>
            {
                if (GetEntity(args.Uid) == uid && _powerReceiverSystem.IsPowered(uid))
                    OnSaveServerPressed(args);
            });
            SubscribeLocalEvent<DnaModifierConsoleComponent, DnaModifierConsoleClearBufferEvent>((uid, component, args) =>
            {
                if (GetEntity(args.Uid) == uid && _powerReceiverSystem.IsPowered(uid))
                    OnClearBufferPressed(args);
            });
            SubscribeLocalEvent<DnaModifierConsoleComponent, DnaModifierConsoleRenameBufferEvent>((uid, component, args) =>
            {
                if (GetEntity(args.Console) == uid && _powerReceiverSystem.IsPowered(uid))
                    OnRenameBufferPressed(args);
            });
            SubscribeLocalEvent<DnaModifierConsoleComponent, DnaModifierConsoleInjectorEvent>((uid, component, args) =>
            {
                if (GetEntity(args.Uid) == uid && _powerReceiverSystem.IsPowered(uid))
                    OnInjectorPressed(args);
            });
            SubscribeLocalEvent<DnaModifierConsoleComponent, DnaModifierInjectBlockEvent>((uid, component, args) =>
            {
                if (GetEntity(args.Uid) == uid && _powerReceiverSystem.IsPowered(uid))
                    OnInjectBlockPressed(args);
            });
            SubscribeLocalEvent<DnaModifierConsoleComponent, DnaModifierConsoleSubjectInjectEvent>((uid, component, args) =>
            {
                if (GetEntity(args.Uid) == uid && _powerReceiverSystem.IsPowered(uid))
                    OnSubjectInjectPressed(args);
            });

            SubscribeLocalEvent<DnaModifierConsoleComponent, DnaModifierConsoleExportOnDiskEvent>((uid, component, args) =>
            {
                if (GetEntity(args.Uid) == uid && _powerReceiverSystem.IsPowered(uid))
                    OnExportOnDiskPressed(args);
            });
            SubscribeLocalEvent<DnaModifierConsoleComponent, DnaModifierConsoleExportFromDiskEvent>((uid, component, args) =>
            {
                if (GetEntity(args.Uid) == uid && _powerReceiverSystem.IsPowered(uid))
                    OnExportFromDiskPressed(args);
            });
            SubscribeLocalEvent<DnaModifierConsoleComponent, DnaModifierConsoleClearDiskEvent>((uid, component, args) =>
            {
                if (GetEntity(args.Uid) == uid && _powerReceiverSystem.IsPowered(uid))
                    OnClearDiskPressed(args);
            });



        }

        #region UI logic
        private void OnInit(EntityUid uid, DnaModifierConsoleComponent component, ComponentInit args)
        {
            component.LastInjectorTime = _timing.CurTime + component.InjectorCooldown;
            component.LastSubjectInjectTime = _timing.CurTime + component.SubjectInjectCooldown;
            _signalSystem.EnsureSourcePorts(uid, DnaModifierConsoleComponent.ScannerPort);

            Dirty(uid, component);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var query = EntityQueryEnumerator<DnaModifierConsoleComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var component, out var transform))
            {
                if (component.NextUpdate > _timing.CurTime)
                    continue;

                component.NextUpdate = _timing.CurTime + component.UpdateInterval;
                if (component.GeneticScanner != null && HasComp<MedicalScannerComponent>(component.GeneticScanner))
                {
                    Transform(component.GeneticScanner.Value).Coordinates.TryDistance(EntityManager, transform.Coordinates, out float scannerDistance);
                    component.GeneticScannerInRange = scannerDistance <= component.MaxDistance;

                    UpdateUserInterface(uid, component);
                }
            }
        }

        private void OnUpdateUI(DnaModifierUpdateEvent args)
        {
            if (!TryComp<DnaModifierConsoleComponent>(GetEntity(args.Uid), out var component))
                return;

            UpdateUserInterface(GetEntity(args.Uid), component);
        }

        private void OnPowerChanged(EntityUid uid, DnaModifierConsoleComponent component, ref PowerChangedEvent args)
        {
            UpdateUserInterface(uid, component);
        }

        private void OnMapInit(EntityUid uid, DnaModifierConsoleComponent component, MapInitEvent args)
        {
            if (!TryComp<DeviceLinkSourceComponent>(uid, out var receiver))
                return;

            foreach (var port in receiver.Outputs.Values.SelectMany(ports => ports))
            {
                if (TryComp<MedicalScannerComponent>(port, out var scanner))
                {
                    component.GeneticScanner = port;
                    scanner.ConnectedConsole = uid;
                }
            }
        }

        private void OnNewLink(EntityUid uid, DnaModifierConsoleComponent component, NewLinkEvent args)
        {
            if (TryComp<MedicalScannerComponent>(args.Sink, out var scanner) && args.SourcePort == DnaModifierConsoleComponent.ScannerPort)
            {
                component.GeneticScanner = args.Sink;
                scanner.ConnectedConsole = uid;
            }

            RecheckConnections(uid, component.GeneticScanner, component);
        }

        private void OnPortDisconnected(EntityUid uid, DnaModifierConsoleComponent component, PortDisconnectedEvent args)
        {
            if (args.Port == DnaModifierConsoleComponent.ScannerPort)
                component.GeneticScanner = null;

            UpdateUserInterface(uid, component);
        }

        private void OnUIOpen(EntityUid uid, DnaModifierConsoleComponent component, AfterActivatableUIOpenEvent args)
        {
            UpdateUserInterface(uid, component);
        }

        private void OnAnchorChanged(EntityUid uid, DnaModifierConsoleComponent component, ref AnchorStateChangedEvent args)
        {
            if (args.Anchored)
            {
                RecheckConnections(uid, component.GeneticScanner, component);
                return;
            }
            UpdateUserInterface(uid, component);
        }

        public void UpdateUserInterface(EntityUid consoleUid, DnaModifierConsoleComponent consoleComponent)
        {
            if (!_uiSystem.HasUi(consoleUid, DnaModifierUiKey.Key))
                return;

            if (!_powerReceiverSystem.IsPowered(consoleUid))
            {
                _uiSystem.CloseUis(consoleUid);
                return;
            }

            var newState = GetUserInterfaceState((consoleUid, consoleComponent));
            _uiSystem.SetUiState(consoleUid, DnaModifierUiKey.Key, newState);
        }

        public void RecheckConnections(EntityUid console, EntityUid? scanner, DnaModifierConsoleComponent? consoleComp = null)
        {
            if (!Resolve(console, ref consoleComp))
                return;

            if (scanner != null)
            {
                Transform(scanner.Value).Coordinates.TryDistance(EntityManager, Transform((console)).Coordinates, out float scannerDistance);
                consoleComp.GeneticScannerInRange = scannerDistance <= consoleComp.MaxDistance;
            }

            UpdateUserInterface(console, consoleComp);
        }

        private DnaModifierBoundUserInterfaceState GetUserInterfaceState(Entity<DnaModifierConsoleComponent> ent)
        {
            // genetic scanner info
            EntityUid? inputContainer = null;
            NetEntity console = GetNetEntity(ent);

            string scanBodyInfo = string.Empty;
            string scannerBodyStatus = string.Empty;
            string scannerBodyDna = string.Empty;
            string scannerSpecies = string.Empty;

            float scannerBodyHealth = -1;
            float scannerBodyRadiation = 0;

            bool hasDisk = false;
            bool scannerHasBeaker = false;
            bool scannerInRange = ent.Comp.GeneticScannerInRange;

            EnzymeInfo? enzyme = null;
            UniqueIdentifiersData? uniqueIdentifiers = null;
            List<EnzymesPrototypeInfo>? enzymesPrototypes = null;

            var currentTime = _timing.CurTime;
            var injectorCooldown = ent.Comp.LastInjectorTime + ent.Comp.InjectorCooldown;
            var subjectInjectCooldown = ent.Comp.LastSubjectInjectTime + ent.Comp.SubjectInjectCooldown;

            var buffer = GetAllBuffers(ent);
            if (ent.Comp.GeneticScanner != null && TryComp<MedicalScannerComponent>(ent.Comp.GeneticScanner, out var scanner))
            {
                EntityUid? scanBody = scanner.BodyContainer.ContainedEntity;
                inputContainer = _itemSlotsSystem.GetItemOrNull(ent.Comp.GeneticScanner.Value, SharedDnaModifier.InputSlotName);

                if (_itemSlotsSystem.TryGetSlot(ent, SharedDnaModifier.DiskSlotName, out var diskSlot)
                    && diskSlot.HasItem && diskSlot.Item != null)
                {
                    hasDisk = true;
                    if (_dnaModifier.TryGetDataFromDisk(diskSlot.Item.Value, out var data))
                        enzyme = data;
                }

                if (_itemSlotsSystem.TryGetSlot(ent.Comp.GeneticScanner.Value, SharedDnaModifier.InputSlotName, out var beakerSlot)
                    && beakerSlot.HasItem)
                {
                    scannerHasBeaker = true;
                }

                // GET STATE
                if (scanBody != null && TryComp<MobStateComponent>(scanBody, out var mobState))
                {
                    scanBodyInfo = MetaData(scanBody.Value).EntityName;
                    scannerBodyStatus = (mobState.CurrentState != MobState.Invalid)
                        ? GetStatus(mobState.CurrentState)
                        : Loc.GetString("dna-modifier-entity-unknown-text");

                    if (TryComp<HumanoidAppearanceComponent>(scanBody.Value, out var humanoid))
                    {
                        if (_prototypeManager.TryIndex(humanoid.Species, out SpeciesPrototype? speciesProto))
                        {
                            scannerSpecies = Loc.GetString(speciesProto.Name);
                        }
                        else
                        {
                            scannerSpecies = humanoid.Species.ToString();
                        }
                    }

                    if (TryComp<DnaComponent>(scanBody.Value, out var dna))
                        scannerBodyDna = GeneticSequence.ToNitrogenBases(dna.DNA ?? string.Empty);

                    if (TryComp<DamageableComponent>(scanBody.Value, out var damage))
                    {
                        if (TryComp<MobThresholdsComponent>(scanBody.Value, out var mobThresholds))
                        {
                            FixedPoint2 deathHealth = FixedPoint2.Zero;
                            foreach (var threshold in mobThresholds.Thresholds)
                            {
                                if (threshold.Value == MobState.Dead)
                                {
                                    deathHealth = threshold.Key;
                                    break;
                                }
                            }

                            if (deathHealth > FixedPoint2.Zero)
                            {
                                float currentHealth = 1.0f - (damage.TotalDamage.Float() / deathHealth.Float());
                                scannerBodyHealth = Math.Clamp(currentHealth, 0f, 1f);
                            }
                        }

                        if (damage.Damage.DamageDict.TryGetValue(RadDamage, out var radiationDamage))
                            scannerBodyRadiation = Math.Clamp(radiationDamage.Float() / 200f, 0f, 1f);
                    }

                    if (TryComp<DnaModifierComponent>(scanBody.Value, out var dnaModifier))
                    {
                        uniqueIdentifiers = dnaModifier.UniqueIdentifiers;
                        enzymesPrototypes = dnaModifier.EnzymesPrototypes;
                    }
                }
            }

            var state = new DnaModifierBoundUserInterfaceState(
                console,
                uniqueIdentifiers,
                enzymesPrototypes,
                enzyme,
                scanBodyInfo,
                scannerBodyStatus,
                scannerBodyDna,
                scannerSpecies,
                scannerBodyHealth,
                scannerBodyRadiation,
                scannerHasBeaker,
                BuildInputContainerInfo(inputContainer),
                scannerInRange,
                hasDisk,
                buffer,
                null,
                currentTime < injectorCooldown ? injectorCooldown - currentTime : TimeSpan.Zero,
                currentTime < subjectInjectCooldown ? subjectInjectCooldown - currentTime : TimeSpan.Zero
            );
            PopulateSequencingState(ent, state);
            return state;
        }

        private string GetStatus(MobState mobState)
        {
            return mobState switch
            {
                MobState.Alive => Loc.GetString("dna-modifier-entity-alive-text"),
                MobState.Critical => Loc.GetString("dna-modifier-entity-critical-text"),
                MobState.Dead => Loc.GetString("dna-modifier-entity-dead-text"),
                _ => Loc.GetString("dna-modifier-entity-unknown-text"),
            };
        }

        private ContainerInfo? BuildInputContainerInfo(EntityUid? container)
        {
            if (container is not { Valid: true })
                return null;

            if (!TryComp(container, out FitsInDispenserComponent? fits)
                || !_solutionContainerSystem.TryGetSolution(container.Value, fits.Solution, out _, out var solution))
            {
                return null;
            }

            return BuildContainerInfo(Name(container.Value), solution);
        }

        private static ContainerInfo BuildContainerInfo(string name, Solution solution)
        {
            return new ContainerInfo(name, solution.Volume, solution.MaxVolume)
            {
                Reagents = solution.Contents
            };
        }

        public Dictionary<int, EnzymeInfo?> GetAllBuffers(EntityUid uid)
        {
            var buffers = new Dictionary<int, EnzymeInfo?>();
            if (!TryComp<DnaClientComponent>(uid, out var client))
                return buffers;

            for (int i = 1; i <= 3; i++)
            {
                if (_dnaClient.TryGetBufferData((uid, client), i, out var data))
                    buffers[i] = data;
            }

            return buffers;
        }
        #endregion

        #region Console logic
        private void PlayClickSound(Entity<DnaModifierConsoleComponent> ent)
            => _audio.PlayPvs(ent.Comp.ClickSound, ent, AudioParams.Default.WithVolume(-2f));

        private void OnEjectPressed(DnaModifierConsoleEjectEvent args)
        {
            if (!TryComp<DnaModifierConsoleComponent>(GetEntity(args.Uid), out var console) || console.GeneticScanner == null)
                return;

            if (!HasComp<MedicalScannerComponent>(console.GeneticScanner))
                return;

            if (_container.TryGetContainer(console.GeneticScanner.Value, SharedDnaModifier.OccupantSlotName, out var container))
                _container.EmptyContainer(container);

            PlayClickSound((GetEntity(args.Uid), console));
            UpdateUserInterface(GetEntity(args.Uid), console);
        }

        private void OnEjectRejuvePressed(DnaModifierConsoleEjectRejuveEvent args)
        {
            if (!TryComp<DnaModifierConsoleComponent>(GetEntity(args.Uid), out var console) || console.GeneticScanner == null)
                return;

            if (!HasComp<MedicalScannerComponent>(console.GeneticScanner))
                return;

            if (_itemSlotsSystem.TryGetSlot(console.GeneticScanner.Value, SharedDnaModifier.InputSlotName, out var slot))
                _itemSlotsSystem.TryEject(console.GeneticScanner.Value, slot, null, out var _, true);

            PlayClickSound((GetEntity(args.Uid), console));
            UpdateUserInterface(GetEntity(args.Uid), console);
        }

        private void OnReagentButtonPressed(DnaModifierConsoleReagentButtonEvent args)
        {
            if (!TryComp<DnaModifierConsoleComponent>(GetEntity(args.Uid), out var console) || console.GeneticScanner == null)
                return;

            if (!TryComp<MedicalScannerComponent>(console.GeneticScanner, out var scanner) || scanner.BodyContainer.ContainedEntity == null
                || !_itemSlotsSystem.TryGetSlot(console.GeneticScanner.Value, SharedDnaModifier.InputSlotName, out var slot))
                return;

            if (slot.Item == null || !HasComp<SolutionContainerManagerComponent>(slot.Item.Value)
                || !_solutionContainerSystem.TryGetSolution(slot.Item.Value, SharedDnaModifier.SolutionSlotName, out var sourceSolution, out var sourceSolutionComp))
                return;

            var targetEntity = scanner.BodyContainer.ContainedEntity.Value;
            if (!HasComp<SolutionContainerManagerComponent>(targetEntity)
                || !_solutionContainerSystem.TryGetInjectableSolution(targetEntity, out var targetSolution, out _))
                return;

            FixedPoint2 transferAmount = args.Amount switch
            {
                DnaModifierReagentAmount.U1 => FixedPoint2.New(1),
                DnaModifierReagentAmount.U5 => FixedPoint2.New(5),
                DnaModifierReagentAmount.U10 => FixedPoint2.New(10),
                DnaModifierReagentAmount.U25 => FixedPoint2.New(25),
                DnaModifierReagentAmount.U50 => FixedPoint2.New(50),
                DnaModifierReagentAmount.U100 => FixedPoint2.New(100),
                DnaModifierReagentAmount.All => sourceSolutionComp.GetReagentQuantity(args.ReagentId),
                _ => FixedPoint2.Zero
            };

            if (transferAmount <= FixedPoint2.Zero || sourceSolutionComp.GetReagentQuantity(args.ReagentId) < transferAmount)
                return;

            var reagentSolution = new Solution();
            reagentSolution.AddReagent(args.ReagentId, transferAmount);

            _solutionContainerSystem.RemoveReagent(sourceSolution.Value, args.ReagentId, transferAmount);
            if (!_solutionContainerSystem.TryAddSolution(targetSolution.Value, reagentSolution))
                return;

            PlayClickSound((GetEntity(args.Uid), console));
            UpdateUserInterface(GetEntity(args.Uid), console);
        }

        private void OnSaveServerPressed(DnaModifierConsoleSaveServerEvent args)
        {
            var clientEntity = GetEntity(args.Uid);
            if (!TryComp<DnaModifierConsoleComponent>(clientEntity, out var console) || console.GeneticScanner == null
                || !TryComp<DnaClientComponent>(clientEntity, out var client))
                return;

            if (!TryComp<MedicalScannerComponent>(console.GeneticScanner, out var scanner))
                return;

            var scanBody = scanner.BodyContainer.ContainedEntity;
            if (!TryComp<DnaModifierComponent>(scanBody, out var dnaModifier))
                return;

            EnzymeInfo? dataToSend = null;
            switch (args.CurrentType)
            {
                case 1:
                    if (dnaModifier.UniqueIdentifiers != null)
                    {
                        dataToSend = new EnzymeInfo()
                        {
                            Identifier = _dnaModifier.CloneUniqueIdentifiers(dnaModifier.UniqueIdentifiers),
                            Info = null
                        };
                    }
                    break;

                case 2:
                    if (dnaModifier.UniqueIdentifiers != null && dnaModifier.EnzymesPrototypes != null)
                    {
                        dataToSend = new EnzymeInfo()
                        {
                            Identifier = _dnaModifier.CloneUniqueIdentifiers(dnaModifier.UniqueIdentifiers),
                            Info = _dnaModifier.CloneEnzymesPrototypes(dnaModifier.EnzymesPrototypes)
                        };
                    }
                    break;

                case 3:
                    if (dnaModifier.EnzymesPrototypes != null)
                    {
                        dataToSend = new EnzymeInfo()
                        {
                            Identifier = null,
                            Info = _dnaModifier.CloneEnzymesPrototypes(dnaModifier.EnzymesPrototypes)
                        };
                    }
                    break;

                default: return;
            }

            if (dataToSend == null)
                return;

            if (dataToSend.Identifier != null && scanBody is { } namedSubject)
                dataToSend.Identifier.EntityName = Name(namedSubject);
            _dnaClient.TryAddToBuffer((clientEntity, client), args.CurrentSection, dataToSend);

            PlayClickSound((GetEntity(args.Uid), console));
            UpdateUserInterface(clientEntity, console);
        }

        private void OnClearBufferPressed(DnaModifierConsoleClearBufferEvent args)
        {
            var clientEntity = GetEntity(args.Uid);
            if (!TryComp<DnaModifierConsoleComponent>(clientEntity, out var console) || !TryComp<DnaClientComponent>(clientEntity, out var client))
                return;

            _dnaClient.TryClearBuffer((clientEntity, client), args.Index);

            PlayClickSound((GetEntity(args.Uid), console));
            UpdateUserInterface(clientEntity, console);
        }

        private void OnRenameBufferPressed(DnaModifierConsoleRenameBufferEvent args)
        {
            var clientEntity = GetEntity(args.Console);
            if (!TryComp<DnaModifierConsoleComponent>(clientEntity, out var console) || !TryComp<DnaClientComponent>(clientEntity, out var client))
                return;

            if (!_dnaClient.TryGetBufferData((clientEntity, client), args.Index, out var data))
                return;

            var user = args.Actor;
            if (!TryComp<ActorComponent>(user, out var playerActor))
                return;

            var playerSession = playerActor.PlayerSession;
            _quickDialog.OpenDialog(playerSession, Loc.GetString("dna-modifier-button-rename"), "",
                (string name) =>
                {
                    var finalName = string.IsNullOrWhiteSpace(name)
                        ? data.SampleName
                        : name;

                    var consolePosition = _transform.GetWorldPosition(clientEntity);
                    var userPosition = _transform.GetWorldPosition(user);
                    var distance = (userPosition - consolePosition).Length();
                    if (distance > 3f)
                        return;

                    _dnaClient.TryRenameBuffer((clientEntity, client), args.Index, finalName);

                    PlayClickSound((clientEntity, console));
                    UpdateUserInterface(clientEntity, console);
                });
        }

        private void OnInjectorPressed(DnaModifierConsoleInjectorEvent args)
        {
            var clientEntity = GetEntity(args.Uid);
            if (!TryComp<DnaModifierConsoleComponent>(clientEntity, out var console) || console.GeneticScanner == null
                || !TryComp<DnaClientComponent>(clientEntity, out var client))
                return;

            if (!_dnaClient.TryGetBufferData((clientEntity, client), args.Index, out var data))
                return;

            if (_timing.CurTime < console.LastInjectorTime + console.InjectorCooldown)
                return;
            var discovered = data.Info?.Where(g => IsDiscovered(g.EnzymesPrototypeId)).ToList();
            if (data.Identifier == null && discovered?.Count is not > 0)
                return;
            _dnaModifier.OnFillingInjector(_entManager.SpawnEntity(Injector, Transform(clientEntity).Coordinates),
                data.Identifier, discovered);

            console.LastInjectorTime = _timing.CurTime;

            PlayClickSound((clientEntity, console));
            UpdateUserInterface(clientEntity, console);
        }

        private void OnInjectBlockPressed(DnaModifierInjectBlockEvent args)
        {
            var clientEntity = GetEntity(args.Uid);
            if (!TryComp<DnaModifierConsoleComponent>(clientEntity, out var console) || console.GeneticScanner == null
                || !TryComp<DnaClientComponent>(clientEntity, out var client))
                return;

            if (!_dnaClient.TryGetBufferData((clientEntity, client), args.Index, out var data) || data.Info == null)
                return;

            var targetBlock = data.Info.FirstOrDefault(e => e.Order == args.CurrentBlock);
            if (targetBlock == null || !IsDiscovered(targetBlock.EnzymesPrototypeId) ||
                _timing.CurTime < console.LastInjectorTime + console.InjectorCooldown)
                return;

            var singleBlockInfo = new List<EnzymesPrototypeInfo> { targetBlock };
            _dnaModifier.OnFillingInjector(_entManager.SpawnEntity(Injector, Transform(clientEntity).Coordinates),
                null, singleBlockInfo);

            console.LastInjectorTime = _timing.CurTime;

            PlayClickSound((clientEntity, console));
            UpdateUserInterface(clientEntity, console);
        }

        private void OnSubjectInjectPressed(DnaModifierConsoleSubjectInjectEvent args)
        {
            var uid = GetEntity(args.Uid);
            if (!TryComp<DnaModifierConsoleComponent>(uid, out var console) ||
                !TrySubject(uid, console, out var subject) ||
                _timing.CurTime < console.LastSubjectInjectTime + console.SubjectInjectCooldown ||
                !_dnaClient.TryGetBufferData(uid, args.Index, out var data))
                return;
            if (data.Identifier is { } appearance)
            {
                BeginAppearance(uid, console, subject, args.Actor, appearance, null);
                _pendingSequences[uid].Structural = data.Info?.Where(g => IsDiscovered(g.EnzymesPrototypeId))
                    .Select(g => (EnzymesPrototypeInfo) g.Clone()).ToList();
                return;
            }
            var sample = new EnzymeInfo { Info = data.Info?.Where(g => IsDiscovered(g.EnzymesPrototypeId)).ToList() };
            _dnaModifier.ChangeDna(subject, sample);
            console.LastSubjectInjectTime = _timing.CurTime;
            PlayClickSound((uid, console));
            UpdateUserInterface(uid, console);
        }

        private void OnExportOnDiskPressed(DnaModifierConsoleExportOnDiskEvent args)
        {
            var clientEntity = GetEntity(args.Uid);
            if (!TryComp<DnaModifierConsoleComponent>(clientEntity, out var console) || console.GeneticScanner == null
                || !TryComp<DnaClientComponent>(clientEntity, out var client))
                return;

            if (!_dnaClient.TryGetBufferData((clientEntity, client), args.Index, out var data))
                return;

            if (_itemSlotsSystem.TryGetSlot(clientEntity, SharedDnaModifier.DiskSlotName, out var diskSlot) && diskSlot.Item != null)
            {
                _dnaModifier.TrySaveInDisk(diskSlot.Item.Value, data);

                PlayClickSound((clientEntity, console));
                UpdateUserInterface(clientEntity, console);
            }
        }

        private void OnExportFromDiskPressed(DnaModifierConsoleExportFromDiskEvent args)
        {
            var clientEntity = GetEntity(args.Uid);
            if (!TryComp<DnaModifierConsoleComponent>(clientEntity, out var console) || console.GeneticScanner == null
                || !TryComp<DnaClientComponent>(clientEntity, out var client))
                return;

            if (_itemSlotsSystem.TryGetSlot(clientEntity, SharedDnaModifier.DiskSlotName, out var diskSlot) && diskSlot.Item != null)
            {
                _dnaModifier.TryGetDataFromDisk(diskSlot.Item.Value, out var data);
                if (data == null)
                    return;

                _dnaClient.TryAddToBufferDisk((clientEntity, client), args.Index, data);

                PlayClickSound((clientEntity, console));
                UpdateUserInterface(clientEntity, console);
            }
        }

        private void OnClearDiskPressed(DnaModifierConsoleClearDiskEvent args)
        {
            var clientEntity = GetEntity(args.Uid);
            if (!TryComp<DnaModifierConsoleComponent>(clientEntity, out var console) || console.GeneticScanner == null)
                return;

            if (_itemSlotsSystem.TryGetSlot(clientEntity, SharedDnaModifier.DiskSlotName, out var diskSlot) && diskSlot.Item != null)
            {
                PlayClickSound((clientEntity, console));
                _dnaModifier.TryClearDiskData(diskSlot.Item.Value);
            }
        }

        #endregion
    }
}
