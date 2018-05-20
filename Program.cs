using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using MHWSharpnessExtractor.DataSources;
using System.Text;
using System.IO;

namespace MHWSharpnessExtractor
{
    class Program
    {
        public enum ResultCode
        {
            Success = 0,
            FormatError = 1,
            OtherError = 2
        }

        static int Main(string[] args)
        {
            return new Program().Run(args).Result;
        }

        private async Task<int> Run(string[] args)
        {
            ResultCode resultCode;
            IDataSource sourceDataSource = new MhwgDotOrg();
            IDataSource targetDataSource = new MhwDbDotCom();

            try
            {
                var sw = Instrumentation.BeginTotalMeasure();

                Task<IList<Weapon>> sourceWeaponsTask = sourceDataSource.ProduceWeaponsAsync();
                Task<IList<Weapon>> targetWeaponsTask = targetDataSource.ProduceWeaponsAsync();

                await Task.WhenAll(sourceWeaponsTask, targetWeaponsTask);

                IList<Weapon> sourceWeapons = sourceWeaponsTask.Result;
                IList<Weapon> targetWeapons = targetWeaponsTask.Result;

                Instrumentation.EndTotalMeasure(sw);

                Console.WriteLine($"Real total time: {Instrumentation.RealTotalTime} ms");

                long total = Instrumentation.NetworkTime + Instrumentation.ProcessingTime;
                double networkPercent = Math.Round(Instrumentation.NetworkTime * 100.0 / total, 2);
                Console.WriteLine($"Network time: {networkPercent}%");
                Console.WriteLine($"Processing time: {100.0 - networkPercent}%");

                Analyze(sourceWeapons, targetWeapons);

				resultCode = ResultCode.Success;
            }
            catch (FormatException ex)
            {
                Console.WriteLine(ex.Message);
                resultCode = ResultCode.FormatError;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                resultCode = ResultCode.OtherError;
            }

            return (int)resultCode;
        }

        private void Analyze(IList<Weapon> sourceWeapons, IList<Weapon> targetWeapons)
        {
            using (TextWriter output = new StreamWriter(Path.Combine(AppContext.BaseDirectory, "report.txt")))
                Analyze(output, sourceWeapons, targetWeapons);
        }

        private void Analyze(TextWriter output, IList<Weapon> sourceWeapons, IList<Weapon> targetWeapons)
        {
            Weapon[] intersect = sourceWeapons.Intersect(targetWeapons).ToArray();

            WeaponType[] weaponTypes = new WeaponType[]
            {
                    WeaponType.GreatSword,
                    WeaponType.LongSword,
                    WeaponType.SwordAndShield,
                    WeaponType.DualBlades,
                    WeaponType.Hammer,
                    WeaponType.HuntingHorn,
                    WeaponType.Lance,
                    WeaponType.Gunlance,
                    WeaponType.SwitchAxe,
                    WeaponType.ChargeBlade,
                    WeaponType.InsectGlaive
            };

            foreach (WeaponType type in weaponTypes)
            {
                IList<Weapon> s = sourceWeapons
                    .Where(x => x.Type == type)
                    .ToList();

                IList<Weapon> t = targetWeapons
                    .Where(x => x.Type == type)
                    .ToList();

                ProcessWeaponCategory(output, type, s, t);
            }
        }

        private void ProcessWeaponCategory(TextWriter output, WeaponType type, IList<Weapon> sourceWeapons, IList<Weapon> targetWeapons)
        {
            //Dictionary<int, Weapon> targetWeaponsDictionary = targetWeapons.ToDictionary(x => x.GetHashCode(), x => x);

            //Dictionary<int, Weapon> targetWeaponsDictionary = new Dictionary<int, Weapon>();
            //foreach (Weapon t in targetWeapons)
            //{
            //    int hashCode;

            //    try
            //    {
            //        hashCode = t.GetHashCode();
            //        targetWeaponsDictionary.Add(hashCode, t);
            //    }
            //    catch (Exception ex)
            //    {
            //        t.GetHashCode();
            //    }
            //}


            //IEnumerable<Weapon> intersect = sourceWeapons.Intersect(targetWeapons, WeaponEqualityComparer.Default);

            //foreach (Weapon source in intersect)
            //{
            //    Weapon matchingTargetWeapon = targetWeaponsDictionary[source.GetHashCode()];
            //    matchingTargetWeapon.UpdateSharpness(source.SharpnessRanks);
            //    output.WriteLine(matchingTargetWeapon.ToJson(true));
            //}

            //output.WriteLine();


            //????????????????????????????????????????????????????


            //output.WriteLine($"{type}:");
            //output.WriteLine();

            //IList<Weapon> sMinusT = sourceWeapons.Except(targetWeapons, WeaponEqualityComparer.Default).ToList();

            //foreach (Weapon weapon in sMinusT)
            //{
            //    IList<(Weapon weapon, int score)> bestMatches = targetWeapons
            //        .Select(x => (weapon: x, score: weapon.ComputeMatchingScore(x)))
            //        .OrderByDescending(x => x.score)
            //        .ToList();

            //    output.WriteLine($"Weapon mismatch {weapon.Name}");

            //    int maxScore = bestMatches[0].score;
            //    for (int i = 0; i < bestMatches.Count; i++)
            //    {
            //        if (bestMatches[i].score != maxScore)
            //            break;

            //        output.WriteLine($"candidate: {bestMatches[i].weapon.Name} (id: {bestMatches[i].weapon.Id})");
            //        bestMatches[i].weapon.PrintMismatches(weapon, output);
            //    }

            //    output.WriteLine();
            //}

            //???????????????????????????????????????????????????????????

            output.WriteLine($"{type}:");
            output.WriteLine();

            foreach (Weapon source in sourceWeapons)
            {
                int sourceHashCode = source.GetHashCode();

                IList<Weapon> allTargets = targetWeapons.Where(t => t.GetHashCode() == sourceHashCode).ToList();

                if (allTargets.Count == 1)
                    output.WriteLine($"{source.Name} : {allTargets[0].Name}");
                else if (allTargets.Count > 1)
                {
                    output.WriteLine($"{source.Name} :");
                    foreach (Weapon possibleWeapon in allTargets)
                        output.WriteLine($"- {possibleWeapon.Name}");
                }
                else if (allTargets.Count == 0)
                {
                    // find best matches
                    IList<(Weapon weapon, int score)> bestMatches = targetWeapons
                        .Select(x => (weapon: x, score: source.ComputeMatchingScore(x)))
                        .OrderByDescending(x => x.score)
                        .ToList();

                    int maxScore = bestMatches[0].score;
                    bestMatches = bestMatches.Where(x => x.score == maxScore).ToList();
                    if (bestMatches.Count == 1)
                        output.WriteLine($"{source.Name} : {bestMatches[0].weapon.Name}");
                    else if (bestMatches.Count > 1)
                    {
                        output.WriteLine($"{source.Name} :");
                        foreach ((Weapon weapon, int) possibleWeapon in bestMatches)
                            output.WriteLine($"- {possibleWeapon.weapon.Name}");
                    }
                }
            }

            output.WriteLine();
            output.WriteLine("----------------------------------------------------------------------------");
            output.WriteLine();
        }
    }
}
