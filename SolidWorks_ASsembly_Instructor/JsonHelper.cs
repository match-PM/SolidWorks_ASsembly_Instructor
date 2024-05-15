using Newtonsoft.Json.Linq;
using SolidWorks.Interop.swconst;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SolidWorks_ASsembly_Instructor
{
    internal class JsonHelper
    {
    }

    public class JsonPathExtractor
    {
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
    }

    public class JsonPathValidator
    {


        public static void ValidateAndAddMissingProperties(JToken tkn2, ref JToken tkn1)
        {
            List<string> tkn2Paths = JsonPathExtractor.GetAllPropertyPaths(tkn2);
            List<string> tkn1Paths = JsonPathExtractor.GetAllPropertyPaths(tkn1);
            //Liste mit Pfaden, die nicht überschreiben werden sollen.
            string keepGUID = "guid";

            //ToDo: Guid abchecken
            //ToDo: Code schön machen und unnötiges rausschmeißen
            //ToDo: Wenn in tkn2 ein element mehr ist, muss das noch abgefangen werden. Namen vergleichen und so 
            string refFramesPath = "mountingDescription.mountingReferences.ref_frames";
            JArray tkn2RefFrameArray;
            JArray tkn1RefFrameArray;
            try
            {
                tkn2RefFrameArray = tkn2.SelectToken(refFramesPath) as JArray;
            }
            catch (Exception ex) { return; }
            try
            {
                tkn1RefFrameArray = tkn1.SelectToken(refFramesPath) as JArray;
            }
            catch (Exception ex) { return; }


            JArray tkn2RefFrameArraysorted = new JArray(tkn2RefFrameArray.OrderBy(obj => (string)obj["name"]));
            JArray tkn1RefFrameArraysorted = new JArray(tkn1RefFrameArray.OrderBy(obj => (string)obj["name"]));
            JArray outputTkn = new JArray();
            JArray compfirst;
            JArray compsecond;
            // The longer array chould be the outer loop Not sure right know if necessary
            if (tkn1RefFrameArraysorted.Count >= tkn2RefFrameArraysorted.Count)
            {
                compfirst = tkn1RefFrameArraysorted;
                compsecond = tkn2RefFrameArraysorted;
            }
            else
            {
                compfirst = tkn2RefFrameArraysorted;
                compsecond = tkn1RefFrameArraysorted;
            }
            
            foreach (JToken nameTkn1 in compfirst)
            {
                bool added = false;
                foreach (JToken nameTkn2 in compsecond)
                {
                    var var1 = (string)nameTkn1["name"];//.SelectToken("name");
                    var var2 = (string)nameTkn2["name"];//.SelectToken("name");
                    if (!(var1 == var2))
                    {
                        continue;
                    }
                    else
                    {
                        if (nameTkn2.SelectToken("constraints.centroid.refFrameNames").HasValues == true)
                        {
                            outputTkn.Add(nameTkn2);
                            added = true;
                            break;
                            //continue;
                        }
                        else
                        {
                            //outputTkn.Add(nameTkn1);
                            //continue;
                        }
                    }

                }
                    if (!added) outputTkn.Add(nameTkn1);
            }

            
            overridePropertyAtPath(tkn1, outputTkn, refFramesPath);


        }

        private static void overridePropertyAtPath(JToken token1, JToken token2, string path)
        {
            if (token1 is null)
            {
                throw new ArgumentNullException(nameof(token1));
            }

            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException($"\"{nameof(path)}\" kann nicht NULL oder leer sein.", nameof(path));
            }

            if (token2 is null)
            {
                throw new ArgumentNullException(nameof(token2));
            }
            JToken transferFrom = token2.SelectToken(path);
            JToken transferTo = token1.SelectToken(path);


            token1.SelectToken(path).Replace(token2);
        }

        private static void AddPropertyAtPath(JToken token1, JToken token2, string path)
        {
            string[] pathParts = path.Split('.');
            JToken currentToken = token1;

            for (int i = 0; i < pathParts.Length; i++)
            {
                string part = pathParts[i];

                if (part.Contains("["))
                {
                    int index = int.Parse(part.Substring(part.IndexOf("[") + 1, part.IndexOf("]") - part.IndexOf("[") - 1));

                    //hier ist was noch nicht ok, brauche ich das überhaupt?
                    if (currentToken[index] == null)
                    {
                        currentToken[index] = new JArray();
                    }

                    //if (i == pathParts.Length - 1)
                    //{
                    //    for (int j = currentToken[index].Count(); j <= index; j++)
                    //    {
                    //        ((JArray)currentToken[index]).Add(token2.SelectToken(path));
                    //    }
                    //}

                    currentToken = currentToken[index];
                }
                else
                {
                    if (currentToken[part] == null)
                    {
                        // Dieser Fall wird vorerst nicht eintreten, da kein Array am Ende steht.
                        //if (i < pathParts.Length - 1 && int.TryParse(pathParts[i + 1], out _))
                        //{
                        //    currentToken[part] = new JArray();
                        //}
                        //else
                        //{
                        if (token2.SelectToken(path.Substring(0, path.LastIndexOf("."))).Type == JTokenType.Array)
                        {
                            currentToken[part] = token2.SelectToken(path.Substring(0, path.LastIndexOf(".")));
                        }
                        else
                        {
                            currentToken[part] = token2.SelectToken(path);
                        }

                        //}
                    }

                    currentToken = currentToken[part];
                }

            }
        }
    }

}
