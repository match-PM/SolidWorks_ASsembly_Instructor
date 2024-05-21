using Newtonsoft.Json.Linq;
using SolidWorks.Interop.swconst;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SolidWorks_ASsembly_Instructor
{
    public class JsonPathExtractor
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
        public static List<string> GetAllPropertyPaths(JToken token)
        {
            // Initialize an empty list to store the property paths
            List<string> paths = new List<string>();

            // Call the recursive helper method to extract all property paths from the token
            ExtractPropertyPaths(token, "", paths);

            // Return the list of property paths
            return paths;
        }

        private static void ExtractPropertyPaths(JToken token, string currentPath, List<string> paths)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    // Iterate through each property of the JSON object
                    foreach (var property in token.Children<JProperty>())
                    {
                        // Construct the new path by appending the property name to the current path
                        string newPath = currentPath + (currentPath == "" ? "" : ".") + property.Name;
                        // Recursively extract paths from the property's value
                        ExtractPropertyPaths(property.Value, newPath, paths);
                    }
                    break;
                case JTokenType.Array:
                    // Iterate through each item in the JSON array
                    int index = 0;
                    foreach (var arrayItem in token.Children())
                    {
                        // Construct the new path by appending the array index to the current path
                        string newPath = currentPath + (currentPath == "" ? "" : ".") + "[" + index + "]";
                        // Recursively extract paths from the array item
                        ExtractPropertyPaths(arrayItem, newPath, paths);
                        index++;
                    }
                    break;
                default:
                    // For primitive values, add the current path to the list of paths
                    paths.Add(currentPath);
                    break;
            }
        }
    }

    public class JsonPathValidator
    {
        public static void ValidateAndAddMissingProperties(JToken tkn2, ref JToken tkn1)
        {
            // Paths for properties that should be retained from the existing JSON
            string keepGUID = "guid";
            string keepSaveDate = "saveDate";

            // Path for the reference frames within the JSON structure
            string refFramesPath = "mountingDescription.mountingReferences.ref_frames";


            // Try to extract the reference frame arrays from both JSON tokens
            JArray tkn2RefFrameArray;
            JArray tkn1RefFrameArray;
            try
            {
                tkn2RefFrameArray = tkn2.SelectToken(refFramesPath) as JArray;
            }
            catch (Exception ex) { return;} // If the path is not found in tkn2, exit the function
            try
            {
                tkn1RefFrameArray = tkn1.SelectToken(refFramesPath) as JArray;
            }
            catch (Exception ex) { return;} // If the path is not found in tkn1, exit the function

            // Sort the reference frame arrays by the "name" property
            JArray tkn2RefFrameArraysorted = new JArray(tkn2RefFrameArray.OrderBy(obj => (string)obj["name"]));
            JArray tkn1RefFrameArraysorted = new JArray(tkn1RefFrameArray.OrderBy(obj => (string)obj["name"]));
            JArray outputTkn = new JArray();
            JArray compFirst;
            JArray compSecond;

            // Determine which array should be the outer loop for comparison
            if (tkn1RefFrameArraysorted.Count >= tkn2RefFrameArraysorted.Count)
            {
                compFirst = tkn1RefFrameArraysorted;
                compSecond = tkn2RefFrameArraysorted;
            }
            else
            {
                compFirst = tkn2RefFrameArraysorted;
                compSecond = tkn1RefFrameArraysorted;
            }

            // Create a set of all unique names from both arrays
            var allNames = new HashSet<string>();
            foreach (var token in compFirst)
            {
                allNames.Add((string)token["name"]);
            }
            foreach (var token in compSecond)
            {
                allNames.Add((string)token["name"]);
            }

            // Iterate through each unique name
            foreach (var name in allNames)
            {
                JToken nameTkn1 = compFirst.FirstOrDefault(t => (string)t["name"] == name);
                JToken nameTkn2 = compSecond.FirstOrDefault(t => (string)t["name"] == name);

                if (nameTkn1 != null && nameTkn2 != null)
                {
                    // If both tokens are present and the second token has "refFrameNames" property with values
                    if (nameTkn2.SelectToken("constraints.centroid.refFrameNames").HasValues == true)
                    {
                        outputTkn.Add(nameTkn2);
                    }
                    else
                    {
                        outputTkn.Add(nameTkn1);
                    }
                }
                else if (nameTkn1 != null)
                {
                    // Only the first token is present
                    outputTkn.Add(nameTkn1);
                }
                else if (nameTkn2 != null)
                {
                    // Only the second token is present
                    outputTkn.Add(nameTkn2);
                }
            }

            // Override the reference frames, GUID, and save date properties in tkn1 with those from tkn2
            overridePropertyAtPath(tkn1, outputTkn, refFramesPath);
            overridePropertyAtPath(tkn1, tkn2.SelectToken(keepGUID), keepGUID);
            overridePropertyAtPath(tkn1, tkn2.SelectToken(keepSaveDate), keepSaveDate);


        }

        private static void overridePropertyAtPath(JToken token1, JToken overrideToken, string path)
        {
            // Validate that token1 is not null
            if (token1 is null)
            {
                throw new ArgumentNullException(nameof(token1));
            }

            // Validate that the path is not null or empty
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException($"\"{nameof(path)}\" kann nicht NULL oder leer sein.", nameof(path));
            }

            // Validate that overrideToken is not null
            if (overrideToken is null)
            {
                throw new ArgumentNullException(nameof(overrideToken));
            }

            // Find the token at the specified path within token1 and replace it with overrideToken
            token1.SelectToken(path).Replace(overrideToken);
        }
    }

}
