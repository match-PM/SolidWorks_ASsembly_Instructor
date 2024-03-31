using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SolidWorks_ASsembly_Instructor
{
    internal class JsonCompare
    {
        //statics
        public static bool CheckFileExists(string filePath, out string fileContent)
        {
            // Überprüfen, ob die Datei existiert
            if (File.Exists(filePath))
            {
                // Dateiinhalt einlesen
                fileContent = File.ReadAllText(filePath);
                return true;
            }
            else
            {
                fileContent = null;
                return false;
            }
        }

        internal static string Compare(string json1, string json2)
        {
            //// JSON-Objekte aus den Strings erstellen
            //JObject obj1 = JObject.Parse(json1);
            //JObject obj2 = JObject.Parse(json2);

            //// Merge-Einstellungen definieren
            //var mergeSettings = new JsonMergeSettings
            //{
            //    // Union-Array-Handhabung, um Duplikate zu vermeiden
            //    MergeArrayHandling = MergeArrayHandling.Union
            //};

            //// JSON-Objekte zusammenführen
            //obj1.Merge(obj2, mergeSettings);

            //// Ergebnis als String ausgeben
            //string mergedJson = obj1.ToString();
            //return mergedJson;

            //// JSON-Objekte aus den Strings erstellen
            //JObject obj1 = JObject.Parse(json1);
            //JObject obj2 = JObject.Parse(json2);

            //// Merge-Einstellungen definieren
            //var mergeSettings = new JsonMergeSettings
            //{
            //    // Union-Array-Handhabung, um Duplikate zu vermeiden
            //    MergeArrayHandling = MergeArrayHandling.Union,
            //    // Überschreibe nur, wenn der Schlüssel im ersten Objekt nicht existiert
            //    MergeNullValueHandling = MergeNullValueHandling.Ignore
            //};

            //// JSON-Objekte zusammenführen
            //obj1.Merge(obj2, mergeSettings);

            //// Ergebnis als String ausgeben
            //string mergedJson = obj1.ToString();
            //return mergedJson;

            // JSON-Objekte aus den Strings erstellen
            JObject obj1 = JObject.Parse(json1);
            JObject obj2 = JObject.Parse(json2);

            // Durch die Eigenschaften des zweiten JSON-Objekts iterieren
            foreach (var property in obj2.Properties())
            {
                // if "constraint" nimm von obj2 && refName.Length != 0

                // Überprüfen, ob die Eigenschaft im ersten JSON-Objekt nicht vorhanden ist
                if (!obj1.ContainsKey(property.Name))
                {
                    // Eigenschaft zum ersten JSON-Objekt hinzufügen
                    obj1.Add(property.Name, property.Value);
                }
            }

            // Ergebnis als String ausgeben
            string mergedJson = obj1.ToString();
            return mergedJson;
        }

        internal static string CompareNested(string json1, string json2)
        {
            // JSON-Objekte aus den Strings erstellen
            JObject obj1 = JObject.Parse(json1);
            JObject obj2 = JObject.Parse(json2);

            // Rekursive Methode zum Vergleichen und Zusammenführen der JSON-Objekte
            MergeJsonObjects(obj1, obj2);

            string mergedJson = obj1.ToString();
            return mergedJson;
        }

        private static void MergeJsonObjects(JObject obj1, JObject obj2)
        {
            foreach (var property in obj2.Properties())
            {
                if (!obj1.ContainsKey(property.Name))
                {
                    obj1.Add(property.Name, property.Value);
                }
                else if (property.Value.Type == JTokenType.Object)
                {
                    JObject nestedObj1 = obj1[property.Name] as JObject;
                    JObject nestedObj2 = property.Value as JObject;

                    if (nestedObj1 != null && nestedObj2 != null)
                    {
                        MergeJsonObjects(nestedObj1, nestedObj2);
                    }
                }
                else if (property.Name == "constraint" && property.Value["refFrameNames"] is JArray refFrameNames && refFrameNames.Count > 0)
                {
                    // Überschreiben Sie das Element "constraint" nicht, wenn die Liste "refFrameNames" nicht leer ist
                    continue;
                }
                else
                {
                    obj1[property.Name] = property.Value;
                }
            }
        }
    }
}

