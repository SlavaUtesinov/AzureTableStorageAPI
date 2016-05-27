using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AzureTableStorageService
{
    public static class AzureConditionBuilderExtention
    {
        private static Dictionary<Type, Func<object, string>> ToAzureType { get; set; }
        private static Dictionary<string, string> ToAzureCondition { get; set; }

        static AzureConditionBuilderExtention()
        {
            ToAzureType = new Dictionary<Type, Func<object, string>>();
            ToAzureType[typeof(string)] = (x) => TableQuery.GenerateFilterCondition("", "", x.ToString());
            ToAzureType[typeof(bool)] = (x) => TableQuery.GenerateFilterConditionForBool("", "", (bool)x);
            ToAzureType[typeof(DateTime)] = (x) => TableQuery.GenerateFilterConditionForDate("", "", ((DateTime)x).Add(TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow)));
            ToAzureType[typeof(double)] = (x) => TableQuery.GenerateFilterConditionForDouble("", "", (double)x);
            ToAzureType[typeof(Guid)] = (x) => TableQuery.GenerateFilterConditionForGuid("", "", (Guid)x);
            ToAzureType[typeof(int)] = (x) => TableQuery.GenerateFilterConditionForInt("", "", (int)x);
            ToAzureType[typeof(long)] = (x) => TableQuery.GenerateFilterConditionForLong("", "", (long)x);
            ToAzureType[typeof(byte[])] = (x) => TableQuery.GenerateFilterConditionForBinary("", "", (byte[])x);

            ToAzureCondition = new Dictionary<string, string>();
            ToAzureCondition["=="] = QueryComparisons.Equal;
            ToAzureCondition[">"] = QueryComparisons.GreaterThan;
            ToAzureCondition[">="] = QueryComparisons.GreaterThanOrEqual;
            ToAzureCondition["<"] = QueryComparisons.LessThan;
            ToAzureCondition["<="] = QueryComparisons.LessThanOrEqual;
            ToAzureCondition["!="] = QueryComparisons.NotEqual;
            ToAzureCondition["AndAlso"] = TableOperators.And;
            ToAzureCondition["OrElse"] = TableOperators.Or;
            ToAzureCondition["Not"] = TableOperators.Not;
        }

        public static string GetAzureCondition<T>(this Expression<Func<T, bool>> predicate)
            where T : class
        {
            string pattern;
            var keys = GetParams(predicate, out pattern);

            var result = predicate.Body.ToString().Replace(predicate.Parameters.First().Name + ".", "").Replace($"{pattern}.", "");
            
            var argsList = new Dictionary<string, object> { { "True", "true" }, { "False", "false" } };
            if (pattern != null)
            {
                var args = keys.Where(x => pattern.Contains(x.GetType().Name)).FirstOrDefault();
                foreach (var item in args.GetType().GetFields().Select(x => new { x.Name, Values = x.GetValue(args) }))
                    argsList.Add(item.Name, item.Values);
            }                

            var i = 1;
            foreach (var field in argsList)
            {
                var value = i++ < 3 ? field.Value.ToString() : GetAzureString(field.Value);
                result = result.Replace(string.Concat(" ", field.Key, ")"), string.Concat(" ", value, ")"));
                result = result.Replace(string.Concat("(", field.Key, " "), string.Concat("(", value, " "));
            }

            return ReplaceLongValues(ReplaceConditions(result).Replace("\"", "\'"));
        }

        static string GetAzureString(object value)
        {
            return ToAzureType[value.GetType()](value).Replace(" ", "");
        }

        static string ReplaceLongValues(string input)
        {
            var result = Regex.Matches(input, @"\s?-?\d{10,}\s?");
            foreach (var match in result)
            {
                var item = (Match)match;
                int temp;
                if ((item.Value.StartsWith(" ") || item.Value.EndsWith(" ")) && !int.TryParse(item.Value, out temp))
                {
                    long longVal;
                    long.TryParse(item.Value, out longVal);
                    var azureFormat = GetAzureString(longVal);

                    if (item.Value.Last() == ' ')
                        azureFormat = azureFormat + " ";
                    else
                        azureFormat = " " + azureFormat;

                    input = input.Replace(item.Value, azureFormat);
                }
            }
            return input;
        }

        static string ReplaceConditions(string condition)
        {
            foreach (var token in ToAzureCondition)
                condition = condition.Replace(" " + token.Key + (token.Key == "Not" ? "" : " "), " " + token.Value + (token.Key == "Not" ? "" : " "));
            return condition;
        }

        static List<object> GetParams<T>(Expression<Func<T, bool>> predicate, out string pattern)
            where T : class
        {
            pattern = null;
            var keys = new List<object>();
            Iterate(predicate.GetType().GetProperty("Body"), predicate, keys, ref pattern);
            return keys.Distinct().ToList();
        }

        static void Iterate(PropertyInfo prop, object model, List<object> keys, ref string pattern)
        {
            var propVal = prop.GetValue(model);
            var typeOfPropVal = propVal.GetType();

            if (typeof(ConstantExpression).IsAssignableFrom(typeOfPropVal))
            {
                keys.Add(typeOfPropVal.GetProperty("Value").GetValue(propVal));

                if (propVal.ToString().Contains("value"))
                    pattern = propVal.ToString();
            }
            else
            {
                var childProps = typeOfPropVal.GetProperties().Where(x => typeof(Expression) == x.PropertyType);
                foreach (var childProp in childProps)
                    Iterate(childProp, propVal, keys, ref pattern);
            }
        }
    }
}
