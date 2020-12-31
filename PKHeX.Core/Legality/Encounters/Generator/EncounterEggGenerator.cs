﻿using System;
using System.Collections.Generic;

using static PKHeX.Core.Legal;

namespace PKHeX.Core
{
    public static class EncounterEggGenerator
    {
        public static IEnumerable<EncounterEgg> GenerateEggs(PKM pkm, bool all = false)
        {
            var table = EvolutionTree.GetEvolutionTree(pkm, Math.Max(2, pkm.Format));
            int maxSpeciesOrigin = GetMaxSpeciesOrigin(pkm.Generation);
            var evos = table.GetValidPreEvolutions(pkm, maxLevel: 100, maxSpeciesOrigin: maxSpeciesOrigin, skipChecks: true);
            return GenerateEggs(pkm, evos, all);
        }

        public static IEnumerable<EncounterEgg> GenerateEggs(PKM pkm, IReadOnlyList<EvoCriteria> chain, bool all = false)
        {
            int species = pkm.Species;
            if (NoHatchFromEgg.Contains(species))
                yield break;

            int gen = pkm.Generation;
            if (gen <= 1)
                yield break; // can't get eggs
            if (NoHatchFromEggForm(species, pkm.Form, gen))
                yield break; // can't originate from eggs

            // version is a true indicator for all generation 3-5 origins
            var ver = (GameVersion)pkm.Version;
            int lvl = gen <= 3 ? 5 : 1;
            int max = GetMaxSpeciesOrigin(gen);

            var e = EvoBase.GetBaseSpecies(chain, 0);
            if (e.Species <= max && !NoHatchFromEggFormGen(e.Species, e.Form, ver))
            {
                yield return new EncounterEgg(e.Species, e.Form, lvl, gen, ver);
                if (gen > 5 && (pkm.WasTradedEgg || all) && HasOtherGamePair(ver))
                    yield return new EncounterEgg(e.Species, e.Form, lvl, gen, GetOtherTradePair(ver));
            }

            if (!Breeding.GetSplitBreedGeneration(gen).Contains(species))
                yield break; // no other possible species

            var o = EvoBase.GetBaseSpecies(chain, 1);
            if (o.Species == e.Species)
                yield break;

            if (o.Species <= max && !NoHatchFromEggFormGen(o.Species, o.Form, ver))
            {
                yield return new EncounterEggSplit(o.Species, o.Form, lvl, gen, ver, e.Species);
                if (gen > 5 && (pkm.WasTradedEgg || all) && HasOtherGamePair(ver))
                    yield return new EncounterEggSplit(o.Species, o.Form, lvl, gen, GetOtherTradePair(ver), e.Species);
            }
        }

        private static bool NoHatchFromEggForm(int species, int form, int gen)
        {
            if (form == 0)
                return false;
            if (FormInfo.IsTotemForm(species, form, gen))
                return true;
            if (species == (int) Species.Pichu)
                return true; // can't get Spiky Ear Pichu eggs
            if (species == (int) Species.Sinistea || species == (int) Species.Polteageist) // Antique = impossible
                return true; // can't get Antique eggs
            return false;
        }

        private static bool NoHatchFromEggFormGen(int species, int form, GameVersion game)
        {
            // Sanity check form for origin
            var gameInfo = GameData.GetPersonal(game);
            var entry = gameInfo.GetFormEntry(species, form);
            return form >= entry.FormCount && !(species == (int)Species.Rotom && form <= 5);
        }

        // Gen6+ update the origin game when hatched. Quick manip for X.Y<->A.O | S.M<->US.UM, ie X->A
        private static GameVersion GetOtherTradePair(GameVersion ver)
        {
            if (ver <= GameVersion.OR) // gen6
                return (GameVersion)((int)ver ^ 2);
            if (ver <= GameVersion.MN) // gen7
                return ver + 2;
            return ver - 2;
        }

        private static bool HasOtherGamePair(GameVersion ver)
        {
            return ver < GameVersion.GP; // lgpe and sw/sh don't have a sister pair
        }
    }

    /// <summary>
    /// Specialized Egg Generator for Gen2
    /// </summary>
    internal static class EncounterEggGenerator2
    {
        public static IEnumerable<EncounterEgg> GenerateEggs(PKM pkm, bool all = false)
        {
            var table = EvolutionTree.GetEvolutionTree(pkm, 2);
            int maxSpeciesOrigin = GetMaxSpeciesOrigin(2);
            var evos = table.GetValidPreEvolutions(pkm, maxLevel: 100, maxSpeciesOrigin: maxSpeciesOrigin, skipChecks: true);
            return GenerateEggs(pkm, evos, all);
        }

        public static IEnumerable<EncounterEgg> GenerateEggs(PKM pkm, IReadOnlyList<EvoCriteria> chain, bool all = false)
        {
            var canBeEgg = all || GetCanBeEgg(pkm);
            if (!canBeEgg)
                yield break;

            var baseID = chain[chain.Count - 1];
            if ((baseID.Species >= MaxSpeciesID_2 || baseID.Form != 0) && chain.Count != 1)
                baseID = chain[chain.Count - 2];
            if (baseID.Form != 0)
                yield break; // Forms don't exist in Gen2, besides Unown (which can't breed). Nothing can form-change.

            int species = baseID.Species;
            if (ParseSettings.AllowGen2Crystal(pkm))
                yield return new EncounterEgg(species, 0, 5, 2, GameVersion.C); // gen2 egg
            yield return new EncounterEgg(species, 0, 5, 2, GameVersion.GS); // gen2 egg
        }

        private static bool GetCanBeEgg(PKM pkm)
        {
            bool canBeEgg = !pkm.Gen1_NotTradeback && GetCanBeEgg2(pkm) && !NoHatchFromEgg.Contains(pkm.Species);
            if (!canBeEgg)
                return false;

            if (!IsEvolutionValid(pkm))
                return false;

            return true;
        }

        private static bool GetCanBeEgg2(PKM pkm)
        {
            if (pkm.IsEgg)
                return pkm.Format == 2;

            if (pkm.Format > 2)
            {
                if (pkm.Met_Level < 5)
                    return false;
            }
            else
            {
                if (pkm.Met_Location != 0 && pkm.Met_Level != 1) // 2->1->2 clears met info
                    return false;
            }

            return pkm.CurrentLevel >= 5;
        }

        private static bool IsEvolutionValid(PKM pkm, int minSpecies = -1, int minLevel = -1)
        {
            var curr = EvolutionChain.GetValidPreEvolutions(pkm, minLevel: minLevel);
            var min = curr.FindLast(z => z.Species == minSpecies);
            if (min != null && min.Level < minLevel)
                return false;
            var poss = EvolutionChain.GetValidPreEvolutions(pkm, maxLevel: 100, minLevel: minLevel, skipChecks: true);

            if (minSpecies != -1)
            {
                int last = poss.FindLastIndex(z => z.Species == minSpecies);
                return curr.Count >= last;
            }
            return curr.Count >= poss.Count;
        }
    }
}
