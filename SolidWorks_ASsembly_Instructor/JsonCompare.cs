using Newtonsoft.Json.Linq;
using SolidWorks.Interop.sldworks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;

namespace SolidWorks_ASsembly_Instructor
{
    internal class JsonCompare
    {
        public event Action<string> DebugLogMessageUpdated;
        public event Action<string> LogMessageUpdated;
        public event Action<string> WarningLogMessageUpdated;
        public event Action<string> ErrorLogMessageUpdated;

        public static List<string> GetAllPropertyPaths(JToken token)
        {
            List<string> paths = new List<string>();
            ExtractPropertyPaths(token, "", paths);
            return paths;
        }

        private static void ExtractPropertyPaths(JToken token, string currentPath, List<string> paths)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    foreach (var property in token.Children<JProperty>())
                    {
                        string newPath = currentPath + (currentPath == "" ? "" : ".") + property.Name;
                        ExtractPropertyPaths(property.Value, newPath, paths);
                    }
                    break;
                case JTokenType.Array:
                    int index = 0;
                    foreach (var arrayItem in token.Children())
                    {
                        string newPath = currentPath + (currentPath == "" ? "" : ".") + "[" + index + "]";
                        ExtractPropertyPaths(arrayItem, newPath, paths);
                        index++;
                    }
                    break;
                default:
                    paths.Add(currentPath);
                    break;
            }
        }



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
            //obj1.Merge(obj2, new JsonMergeSettings
            //{
            //    // union array values together to avoid duplicates
            //    MergeArrayHandling = MergeArrayHandling.Union
            //});



            JToken output = obj1 as JToken;
            JsonPathValidator.ValidateAndAddMissingProperties(obj2, ref output);
            //checkForDublicates(obj1, pathToArray);
            //Debug.WriteLine(string.Join("\r\n", GetAllPropertyPaths(obj1)));
            //Debug.Write(obj1);

            //string mergedJson = obj1.ToString();
            string mergedJson = output.ToString();
            return mergedJson;
        }

        private static void checkForDublicates(JObject obj1, string pathToArray)
        {
            
            JArray components = obj1.SelectToken(pathToArray) as JArray;
            // Debug.Write($"{components.Path} -> {components.ToString()}");

            List<JObject> visitedComponents = new List<JObject>();
            List<string> visitedComponentsNames = new List<string>();
            foreach (JObject component in components)
            {
                if (visitedComponentsNames.Contains(component["name"].ToString()))
                {
                    int index = visitedComponentsNames.IndexOf(component["name"].ToString());
                    if (component.Children().Count() > visitedComponents[index].Children().Count())
                    {
                        visitedComponents.RemoveAt(index);
                        visitedComponentsNames.RemoveAt(index);
                        visitedComponents.Insert(index, component);
                        visitedComponentsNames.Insert(index, component["name"].ToString());
                        Debug.Write("We have a new element here");
                    }
                }
                else
                {
                    visitedComponents.Add(component);
                    visitedComponentsNames.Add(component["name"].ToString());
                }

            }
            //JArray comp = obj1["mountingDescription"]["mountingReferences"]["ref_planes"] as JArray;
            components.RemoveAll();
            foreach (JObject vComp in visitedComponents)
            {
                components.Add(vComp);
            }
        }

        private static JObject FindDiff(JToken Current, JToken Model)
        {
            var diff = new JObject();
            if (JToken.DeepEquals(Current, Model)) return diff;

            switch (Current.Type)
            {
                case JTokenType.Object:
                    {
                        var current = Current as JObject;
                        var model = Model as JObject;
                        var addedKeys = current.Properties().Select(c => c.Name).Except(model.Properties().Select(c => c.Name));
                        var removedKeys = model.Properties().Select(c => c.Name).Except(current.Properties().Select(c => c.Name));
                        var unchangedKeys = current.Properties().Where(c => JToken.DeepEquals(c.Value, Model[c.Name])).Select(c => c.Name);
                        foreach (var k in addedKeys)
                        {
                            diff[k] = new JObject
                            {
                                ["+"] = Current[k]
                            };
                        }
                        foreach (var k in removedKeys)
                        {
                            diff[k] = new JObject
                            {
                                ["-"] = Model[k]
                            };
                            //diff[k] = Model[k];
                        }
                        var potentiallyModifiedKeys = current.Properties().Select(c => c.Name).Except(addedKeys).Except(unchangedKeys);
                        foreach (var k in potentiallyModifiedKeys)
                        {
                            var foundDiff = FindDiff(current[k], model[k]);
                            if (foundDiff.HasValues) diff[k] = foundDiff;
                        }
                    }
                    break;
                case JTokenType.Array:
                    {
                        var current = Current as JArray;
                        var model = Model as JArray;
                        for (var i = 0; i < current.Children().Count(); i++)
                        {
                            //Debug.Write(current[i] + "\r\n");
                            //Debug.Write(model[i] + "\r\n");
                            var foundDiff = FindDiff(current[i], model[i]);
                        }

                        var plus = new JArray(current.Except(model, new JTokenEqualityComparer()));
                        var minus = new JArray(model.Except(current, new JTokenEqualityComparer()));
                        if (plus.HasValues) diff["+"] = plus;
                        if (minus.HasValues) diff["-"] = minus;
                        //if (minus.HasValues) diff[currentProperty] = minus;
                    }
                    break;
                default:
                    diff["+"] = Current;
                    diff["-"] = Model;
                    break;
            }

            return diff;
        }


        private static void MergeJsonObjects(JObject obj1, JObject obj2)
        {
            foreach (var property in obj2.Properties())
            {

                if (!JToken.DeepEquals(obj1, obj2))
                {
                    JObject nestedObj1 = obj1.GetValue(property.Name) as JObject;
                    JObject nestedObj2 = obj2.GetValue(property.Name) as JObject;
                    //Debug.Write($"Values nicht gleich {property.Name}");
                    //JObject nestedObj1 = obj1.GetValue(property.Name) as JObject;
                    //JObject nestedObj2 = obj2.GetValue(property.Name) as JObject;
                    if (nestedObj1 != null && nestedObj2 != null)
                    {
                        MergeJsonObjects(nestedObj1, nestedObj2);
                    }
                }
                //if (!obj1.ContainsKey(property.Name))
                //{
                //    Debug.Write($"{property.Name} not in obj1, adding it");
                //    obj1.Add(property.Name, property.Value);
                //}
                //else
                //{
                //    JObject nestedObj1 = obj1.GetValue(property.Name) as JObject;
                //    JObject nestedObj2 = obj2.GetValue(property.Name) as JObject;
                //    if (nestedObj1 != null && nestedObj2 != null)
                //    {
                //        MergeJsonObjects(nestedObj1, nestedObj2);
                //    }
                //}

                //if ( false)
                //{
                //    JObject nestedObj1 = obj1[property.Name] as JObject;
                //    JObject nestedObj2 = property.Value as JObject;
                //    MergeJsonObjects(nestedObj1, nestedObj2);
                //}
                //if (!obj1.ContainsKey(property.Name))
                //{
                //    Debug.Write("");
                //    Console.WriteLine($"{property.Name} not in New Json");
                //    //obj1.Add(property.Name, property.Value);
                //}
                //else if (property.Value.Type == JTokenType.Object)
                //{
                //    JObject nestedObj1 = obj1[property.Name] as JObject;
                //    JObject nestedObj2 = property.Value as JObject;

                //    if (nestedObj1 != null && nestedObj2 != null)
                //    {
                //        MergeJsonObjects(nestedObj1, nestedObj2);
                //    }
                //}
                //else if (property.Name == "constraints" && property.Value["refFrameNames"] is JArray refFrameNames && refFrameNames.Count > 0)
                //{
                //    // Überschreiben Sie das Element "constraint" nicht, wenn die Liste "refFrameNames" nicht leer ist
                //    continue;
                //}
                //else
                //{
                //    obj1[property.Name] = property.Value;
                //}
            }
        }

    }
}

