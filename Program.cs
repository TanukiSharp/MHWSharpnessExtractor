using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using MHWSharpnessExtractor.DataSources;
using System.Text;

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
            Weapon[] intersect = sourceWeapons.Intersect(targetWeapons, WeaponEqualityComparer.Default).ToArray();

            Console.WriteLine();
            Console.WriteLine($"Total intersect is {intersect.Length}");
            Console.WriteLine();

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

            var report = new StringBuilder();
            var markdownTableRows = new StringBuilder();

            foreach (WeaponType type in weaponTypes)
            {
                Weapon[] s = sourceWeapons
                    .Where(x => x.Type == type)
                    .ToArray();

                Weapon[] t = targetWeapons
                    .Where(x => x.Type == type)
                    .ToArray();

                report.AppendLine($"{type}:");

                int len = Math.Min(s.Length, t.Length);
                for (int i = 0; i < len; i++)
                {
                    if (s[i].Equals(t[i]) == false)
                        report.AppendLine($"    [{i}] {t[i].Name}");
                }

                Weapon[] sMinusT = s.Except(t, WeaponEqualityComparer.Default).ToArray();
                Weapon[] tMinusS = t.Except(s, WeaponEqualityComparer.Default).ToArray();

                Weapon[] perTypeIntersect = s
                    .Intersect(t, WeaponEqualityComparer.Default)
                    .ToArray();

                markdownTableRows.AppendLine($"| {type} | {s.Length} | {t.Length} | {t.Length - s.Length} | {sMinusT.Length} | {tMinusS.Length} | {perTypeIntersect.Length} |");
            }

            Console.WriteLine(report.ToString());
            Console.WriteLine(markdownTableRows.ToString());
        }
    }
}
