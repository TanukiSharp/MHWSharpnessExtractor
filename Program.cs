using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using MHWSharpnessExtractor.DataSources;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using System.Diagnostics;

namespace MHWSharpnessExtractor
{
    class Program
    {
        public enum ResultCode
        {
            Success = 0,
            FormatError = 1,
            NetworkError = 2,
            OtherError = 3
        }

        static int Main(string[] args)
        {
            return new Program().Run(args).Result;
        }

        private const string WeaponNameMappingFilename = "mhwg_to_mhwdb.json";
        private const string WeaponSharpnessFilename = "weapon_sharpness.json";

        private async Task<int> Run(string[] args)
        {
            ResultCode resultCode;
            IDataSource sourceDataSource = new MhwgDotOrg();
            IDataSource targetDataSource = new MhwDbDotCom();

            bool noNameMapping = args.Contains("--no-name-mapping");
            bool noSharpness = args.Contains("--no-sharpness");
            bool isSilent = args.Contains("--silent");

            try
            {
                Stopwatch sw = Instrumentation.BeginTotalMeasure();

                Task<IList<Weapon>> sourceWeaponsTask = sourceDataSource.ProduceWeaponsAsync();
                Task<IList<Weapon>> targetWeaponsTask = targetDataSource.ProduceWeaponsAsync();

                await Task.WhenAll(sourceWeaponsTask, targetWeaponsTask);

                IList<Weapon> sourceWeapons = sourceWeaponsTask.Result;
                IList<Weapon> targetWeapons = targetWeaponsTask.Result;

                Instrumentation.EndTotalMeasure(sw);

                if (isSilent == false)
                {
                    Console.WriteLine($"Real total time: {Instrumentation.RealTotalTime} ms");

                    long total = Instrumentation.NetworkTime + Instrumentation.ProcessingTime;
                    double networkPercent = Math.Round(Instrumentation.NetworkTime * 100.0 / total, 2);
                    Console.WriteLine($"Network time: {networkPercent}%");
                    Console.WriteLine($"Processing time: {100.0 - networkPercent}%");
                }

                if (noNameMapping == false)
                    GenerateWeaponsNameMapping(sourceWeapons, targetWeapons);

                if (noSharpness == false)
                    GenerateShaprnessOutput(sourceWeapons, targetWeapons);

                resultCode = ResultCode.Success;
            }
            catch (FormatException ex)
            {
                Console.WriteLine(ex);
                resultCode = ResultCode.FormatError;
            }
            catch (TaskCanceledException ex)
            {
                Console.WriteLine(ex.Message);
                resultCode = ResultCode.NetworkError;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                resultCode = ResultCode.OtherError;
            }

            return (int)resultCode;
        }

        private void GenerateWeaponsNameMapping(IList<Weapon> sourceWeapons, IList<Weapon> targetWeapons)
        {
            using (TextWriter output = new StreamWriter(Path.Combine(AppContext.BaseDirectory, WeaponNameMappingFilename)))
                GenerateWeaponsNameMapping(output, sourceWeapons, targetWeapons);
        }

        private void GenerateWeaponsNameMapping(TextWriter output, IList<Weapon> sourceWeapons, IList<Weapon> targetWeapons)
        {
            var weaponTypes = new WeaponType[]
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

            var sb = new StringBuilder();
            sb.Append("{\n");

            foreach (WeaponType type in weaponTypes)
            {
                IList<Weapon> s = sourceWeapons
                    .Where(x => x.Type == type)
                    .ToList();

                IList<Weapon> t = targetWeapons
                    .Where(x => x.Type == type)
                    .ToList();

                ProcessWeaponCategory(sb, type, s, t);
            }

            sb.Remove(sb.Length - 2, 2);
            sb.Append("\n}\n");

            output.WriteLine(sb.ToString());
        }

        private static string Escape(string str)
        {
            return str.Replace("\"", "\\\"");
        }

        private void ProcessWeaponCategory(StringBuilder sb, WeaponType type, IList<Weapon> sourceWeapons, IList<Weapon> targetWeapons)
        {
            foreach (Weapon source in sourceWeapons)
            {
                IList<(Weapon weapon, int score)> bestMatches = targetWeapons
                    .Select(x => (weapon: x, score: source.ComputeMatchingScore(x)))
                    .OrderByDescending(x => x.score)
                    .ToList();

                int maxScore = bestMatches[0].score;
                bestMatches = bestMatches.Where(x => x.score == maxScore).ToList();
                if (bestMatches.Count == 1)
                    sb.Append($"    \"{Escape(source.Name)}\": \"{Escape(bestMatches[0].weapon.Name)}\",\n");
                else if (bestMatches.Count > 1)
                {
                    sb.Append($"    \"{Escape(source.Name)}\": [\n");
                    foreach ((Weapon weapon, int) possibleWeapon in bestMatches)
                        sb.Append($"        \"{Escape(possibleWeapon.weapon.Name)}\",\n");
                    sb.Remove(sb.Length - 2, 2);
                    sb.Append("\n    ],\n");
                }
            }
        }

        private void GenerateShaprnessOutput(IList<Weapon> allSourceWeapons, IList<Weapon> allTargetWeapons)
        {
            using (TextWriter output = new StreamWriter(Path.Combine(AppContext.BaseDirectory, WeaponSharpnessFilename)))
                GenerateShaprnessOutput(output, allSourceWeapons, allTargetWeapons);
        }

        private void GenerateShaprnessOutput(TextWriter output, IList<Weapon> allSourceWeapons, IList<Weapon> allTargetWeapons)
        {
            string mappingContent = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", WeaponNameMappingFilename));
            IDictionary<string, string> mapping = JsonConvert.DeserializeObject<IDictionary<string, string>>(mappingContent);

            Dictionary<string, Weapon> targetWeapons = allTargetWeapons.ToDictionary(x => x.Name);

            var result = new List<object>();

            foreach (Weapon sourceWeapon in allSourceWeapons)
            {
                if (mapping.TryGetValue(sourceWeapon.Name, out string mhwdbWeaponName) == false)
                    continue;

                Weapon targetWeapon = targetWeapons[mhwdbWeaponName];

                result.Add(targetWeapon
                    .UpdateSharpnessLevel5(sourceWeapon.SharpnessRanksLevel5)
                    .ToJsonObject()
                );
            }

            string resultJson = JsonConvert.SerializeObject(result);

            output.WriteLine(resultJson);
        }
    }
}
