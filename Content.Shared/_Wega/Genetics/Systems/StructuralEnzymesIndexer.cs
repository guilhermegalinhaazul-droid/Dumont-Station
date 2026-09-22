using System.Linq;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.Genetics.Systems
{
    public sealed class StructuralEnzymesIndexerSystem : EntitySystem
    {
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IRobustRandom _random = default!;

        public const string SpeciesGene = "Species";

        private List<EnzymesPrototypeInfo> _enzymesPrototypes = new List<EnzymesPrototypeInfo>();
        private bool _isInitialized = false;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        }

        private void OnRoundRestart(RoundRestartCleanupEvent args)
        {
            _isInitialized = false;
            _enzymesPrototypes.Clear();
        }

        public List<EnzymesPrototypeInfo> GetAllEnzymesPrototypes()
        {
            if (!_isInitialized)
            {
                InitializeEnzymesPrototypes();
            }

            return _enzymesPrototypes;
        }

        private void InitializeEnzymesPrototypes()
        {
            _enzymesPrototypes.Clear();

            var allEnzymesPrototypes = _prototypeManager.EnumeratePrototypes<StructuralEnzymesPrototype>().ToList();
            _random.Shuffle(allEnzymesPrototypes);

            var replaced = allEnzymesPrototypes.SelectMany(p => p.Replaces).ToHashSet();
            allEnzymesPrototypes.RemoveAll(p => replaced.Contains(p.ID));
            foreach (var prototype in allEnzymesPrototypes)
            {
                _enzymesPrototypes.Add(new EnzymesPrototypeInfo
                {
                    EnzymesPrototypeId = prototype.ID,
                    Order = _enzymesPrototypes.Count + 1
                });
            }

            // Wega's former block 55: always last, independent of catalogue size.
            _enzymesPrototypes.Add(new EnzymesPrototypeInfo
            {
                EnzymesPrototypeId = SpeciesGene,
                Order = _enzymesPrototypes.Count + 1
            });

            _isInitialized = true;
        }
    }
}
