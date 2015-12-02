using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml.Linq;

namespace NHSdwnlds
{
    class Program
    {
        /// <summary>
        /// Application's entry point
        /// </summary>
        /// <param name="args">command line arguments</param>
        /// <returns>
        /// 0 - everything is okay
        /// 1 - unknown error
        /// 2 - unknown command line arguments
        /// 3 - couldn't create some directory
        /// 4 - files do not exist on the disk, probably couldn't download
        /// 5 - error reading files
        /// </returns>
        static int Main(string[] args)
        {
            try
            {
                #region some settings
                /// <summary>
                /// should the app skip the downloading part
                /// </summary>
                bool skipDownloading = false;
                /// <summary>
                /// relative name of the directory for files
                /// </summary>
                string dirName = Properties.Settings.Default.dirName;
                /// <summary>
                /// path to ADD file
                /// </summary>
                string addFilePath = Path.Combine(dirName, "add.csv");
                /// <summary>
                /// path to PDP file
                /// </summary>
                string pdpFilePath = Path.Combine(dirName, "pdp.csv");
                /// <summary>
                /// relative path of the directory for XMLs
                /// </summary>
                string dirNameXML = Properties.Settings.Default.dirNameXML;
                #endregion

                // some primitive checking command line arguments
                if (args.Count() > 0)
                {
                    // help
                    if (args[0] == "/?" || args[0] == "-h" || args[0] == "--help")
                    {
                        StringBuilder helpStr = new StringBuilder()
                            .Append("\nThe application downloads NHS files and does some work with them.\n")
                            .Append("Command line arguments are:\n")
                            .Append("  /?, -h, --help : shows this help\n")
                            .Append("  /f : skips the downloading and parsing parts assuming that you have already parsed the files\n");
                        Console.WriteLine(helpStr.ToString());
                        return 0;
                    }

                    // skip the downloading part
                    if (args[0] == "-f")
                    {
                        skipDownloading = true;
                    }
                    else // unknown arguments
                    {
                        Console.WriteLine("\nUnknown command line arguments! Check help by starting the app with /?");
                        return 2;
                    }
                }

                //
                // --- downloading the files
                //

                // creating some dirs
                if (!Directory.Exists(dirNameXML))
                {
                    try { Directory.CreateDirectory(dirNameXML); }
                    catch (Exception ex)
                    {
                        Console.WriteLine(string.Format("Couldn't create a dir for XMLs! {0}\n", ex.Message));
                        return 3;
                    }
                }
                //string dir4practices = Path.Combine(dirNameXML, "practices");
                //if (!Directory.Exists(dir4practices))
                //{
                //    try { Directory.CreateDirectory(dir4practices); }
                //    catch (Exception ex)
                //    {
                //        Console.WriteLine(string.Format("Couldn't create a dir for practices XMLs! {0}\n", ex.Message));
                //        return 3;
                //    }
                //}
                string dir4pdps = Path.Combine(dirNameXML, "drugs");
                if (!Directory.Exists(dir4pdps))
                {
                    try { Directory.CreateDirectory(dir4pdps); }
                    catch (Exception ex)
                    {
                        Console.WriteLine(string.Format("Couldn't create a dir for pdp XMLs! {0}\n", ex.Message));
                        return 3;
                    }
                }
                string practicesFilePath = Path.Combine(dirNameXML, "practices.xml");

                if (!skipDownloading)
                {
                    // delete previous files
                    try
                    {
                        DirectoryInfo directory2delete = new DirectoryInfo(dir4pdps);
                        foreach (FileInfo file in directory2delete.GetFiles())
                        {
                            if (file.Name.StartsWith("drugs")) { file.Delete(); }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(string.Format("\nCouldn't delete previous files! {0}\n", ex.Message));
                    }

                    // create dir if it doesn't exist
                    if (!Directory.Exists(dirName))
                    {
                        try { Directory.CreateDirectory(dirName); }
                        catch (Exception ex)
                        {
                            Console.WriteLine(string.Format("Couldn't create a dir for files! {0}\n", ex.Message));
                            return 3;
                        }
                    }

                    Console.WriteLine("\nFile downloading starts in a second\n");

                    // ADD file
                    Console.WriteLine(string.Format("(1/2)\n{0} - Downloading the ADD file...", DateTime.Now.ToString("HH:mm:ss")));
                    try { downloadTheFile(Properties.Settings.Default.link2add, addFilePath); }
                    catch (Exception ex)
                    {
                        Console.WriteLine(string.Format("\nCouldn't download ADD file! {0}\n", ex.Message));
                        return 4;
                    }
                    FileInfo archADD = new FileInfo(addFilePath);
                    string fileSizeMBADD = (archADD.Length / 1024 / 1024).ToString();
                    Console.WriteLine(string.Format("{0} - ADD file has been successfully downloaded, its rough size: {1} MB.", DateTime.Now.ToString("HH:mm:ss"), fileSizeMBADD));

                    // PDP file
                    Console.WriteLine(string.Format("(2/2)\n{0} - Downloading the PDP file...", DateTime.Now.ToString("HH:mm:ss")));
                    try { downloadTheFile(Properties.Settings.Default.link2pdp, pdpFilePath); }
                    catch (Exception ex)
                    {
                        Console.WriteLine(string.Format("\nCouldn't download PDP file! {0}\n", ex.Message));
                        return 4;
                    }

                    FileInfo archPDP = new FileInfo(pdpFilePath);
                    string fileSizeMBPDP = (archPDP.Length / 1024 / 1024).ToString();
                    Console.WriteLine(string.Format("{0} - PDP file has been successfully downloaded, its rough size: {1} MB.", DateTime.Now.ToString("HH:mm:ss"), fileSizeMBPDP));

                    //
                    // --- reading the files into XML
                    //

                    Console.WriteLine("\nFile parsing starts in a second\n");
                    Console.WriteLine(string.Format("(1/2)\n{0} - Reading the ADD file...", DateTime.Now.ToString("HH:mm:ss")));
                    // gather all practices
                    //HashSet<string> practices = new HashSet<string>();
                    // add unknown practice (with unknown ID)
                    //practices.Add("unknown");
                    try // reading ADD file
                    {
                        // and also gather all regions
                        // [bad idea] HashSet<string> regions = new HashSet<string>();

                        XDocument doc = new XDocument(new XElement("practices"));
                        var lines = File.ReadLines(addFilePath);
                        foreach (string line in lines)
                        {
                            try // split line into fields
                            {
                                string[] fields = line.Split(',');
                                doc.Element("practices").Add(new XElement("practice",
                                    new XElement("index", fields[0].Trim()),
                                    new XElement("id", fields[1].Trim()),
                                    new XElement("name", fields[2].Trim()),
                                    new XElement("facility", fields[3].Trim()),
                                    new XElement("address1", fields[4].Trim()),
                                    new XElement("address2", fields[5].Trim()),
                                    new XElement("region", fields[6].Trim()),
                                    new XElement("postcode", fields[7].Trim())
                                    ));
                                // [bad idea] regions.Add(fields[6].Trim());
                                //practices.Add(fields[1].Trim());
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(string.Format("{0} - error splitting the line. {1}", DateTime.Now.ToString("HH:mm:ss"), ex.Message));
                                continue;
                            }
                        }
                        doc.Save(practicesFilePath);

                        // [bad idea] create dirs for regions
                        //foreach(string region in regions)
                        //{
                        //    string dirRegion = Path.Combine(dirNameXML, region);
                        //    if (!Directory.Exists(dirRegion))
                        //    {
                        //        Directory.CreateDirectory(dirRegion);
                        //    }
                        //}
                        // create dirs for practices
                        //foreach (string practice in practices)
                        //{
                        //    XDocument docPractice = new XDocument(new XElement("drugs"));
                        //    docPractice.Save(Path.Combine(dir4practices, string.Format("{0}.xml", practice)));
                        //}

                        Console.WriteLine(string.Format("{0} - The ADD file has been successfully read.", DateTime.Now.ToString("HH:mm:ss")));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(string.Format("{0} - error reading the ADD file. {1}", DateTime.Now.ToString("HH:mm:ss"), ex.Message));
                        return 5;
                    }

                    Console.WriteLine(string.Format("(2/2)\n{0} - Reading the PDP file...", DateTime.Now.ToString("HH:mm:ss")));
                    try // reading PDP file
                    {
                        #region read and parse the PDP file into single XML via buffer
                        //// read lines from original CSV
                        //var lines = File.ReadLines(pdpFilePath);
                        //int buffer = 10000, // if RAM consumption is huge, then decrease this value
                        //    itr = 0,
                        //    cnt = 0;
                        //string drugsFileName = Path.Combine(dirNameXML, "pdp.xml");
                        ////string drugsFileName = Path.Combine(dirNameXML, "pdp");
                        ////XDocument doc = new XDocument(new XElement("drugs"));
                        //using (StreamWriter writetext = File.CreateText(drugsFileName))
                        //{
                        //    writetext.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<drugs>");
                        //}
                        ////int linesCount = lines.Count();
                        ////for (int i = 1; i < linesCount; i++) // skip the "head"
                        //XElement drugs = new XElement("drugs");
                        //foreach (string line in lines.Skip(1))
                        //{
                        //    try // split line into fields
                        //    {
                        //        string[] fields = line.Split(',');
                        //        //doc.Element("drugs").Add(new XElement("drug",
                        //        drugs.Add(new XElement("drug",
                        //            new XElement("sha", fields[0].Trim()),
                        //            new XElement("pct", fields[1].Trim()),
                        //            new XElement("practice_id", fields[2].Trim()),
                        //            new XElement("bnf_code", fields[3].Trim()),
                        //            new XElement("bnf_name", fields[4].Trim()),
                        //            new XElement("items", fields[5].Trim()),
                        //            new XElement("nic", fields[6].Trim()),
                        //            new XElement("act_cost", fields[7].Trim()),
                        //            new XElement("period", fields[8].Trim())
                        //            ));

                        //        itr++;
                        //        if (itr >= buffer)
                        //        {
                        //            cnt++;
                        //            // debug
                        //            //if (cnt > 2) { break; }
                        //            itr = 0;
                        //            //doc.Save(string.Format("{0}{1}.xml", drugsFileName, cnt));
                        //            //doc = new XDocument(new XElement("drugs"));
                        //            using (StreamWriter writetext = File.AppendText(drugsFileName))
                        //            {
                        //                // well, not really a beautiful solution
                        //                writetext.WriteLine(drugs.ToString().Replace("<drugs>", "").Replace("</drugs>", "").Trim());
                        //            }
                        //            drugs = new XElement("drugs");
                        //        }
                        //    }
                        //    catch (Exception ex)
                        //    {
                        //        Console.WriteLine(string.Format("{0} - error splitting the line. {1}", DateTime.Now.ToString("HH:mm:ss"), ex.Message));
                        //        continue;
                        //    }
                        //}
                        ////doc.Save(string.Format("{0}{1}.xml", drugsFileName, cnt));
                        //using (StreamWriter writetext = File.AppendText(drugsFileName))
                        //{
                        //    writetext.WriteLine("</drugs>");
                        //}
                        //Console.WriteLine(string.Format("{0} - The PDP file has been successfully read.", DateTime.Now.ToString("HH:mm:ss")));
                        #endregion

                        #region read and parse the PDP file into several XMLs
                        // read lines from original CSV
                        var lines = File.ReadLines(pdpFilePath);
                        int buffer = Properties.Settings.Default.readingBuffer, // if RAM consumption is huge, then decrease this value (or increase, if the opposite)
                            itr = 0,
                            cnt = 0;
                        string drugsFileName = Path.Combine(dir4pdps, "drugs");
                        XDocument doc = new XDocument(new XElement("drugs"));
                        foreach (string line in lines.Skip(1))
                        {
                            try // split line into fields
                            {
                                string[] fields = line.Split(',');
                                doc.Element("drugs").Add(new XElement("drug",
                                    new XElement("sha", fields[0].Trim()),
                                    new XElement("pct", fields[1].Trim()),
                                    new XElement("practice_id", fields[2].Trim()),
                                    new XElement("bnf_code", fields[3].Trim()),
                                    new XElement("bnf_name", fields[4].Trim()),
                                    new XElement("items", fields[5].Trim()),
                                    new XElement("nic", fields[6].Trim()),
                                    new XElement("act_cost", fields[7].Trim()),
                                    new XElement("period", fields[8].Trim())
                                    ));

                                itr++;
                                if (itr >= buffer)
                                {
                                    cnt++;
                                    // debug
                                    //if (cnt > 2) { break; }
                                    itr = 0;
                                    doc.Save(string.Format("{0}{1}.xml", drugsFileName, cnt));
                                    doc = new XDocument(new XElement("drugs"));
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(string.Format("{0} - error splitting the line. {1}", DateTime.Now.ToString("HH:mm:ss"), ex.Message));
                                continue;
                            }
                        }
                        doc.Save(string.Format("{0}{1}.xml", drugsFileName, cnt));
                        Console.WriteLine(string.Format("{0} - The PDP file has been successfully read.", DateTime.Now.ToString("HH:mm:ss")));
                        #endregion

                        #region [bad idea] split all data into XMLs by practices
                        //var lines = File.ReadLines(pdpFilePath);
                        //// iterate through the list of practices
                        //foreach (string practice in practices)
                        //{
                        //    //string practiceFileName = Path.Combine(dir4practices, string.Format("{0}.xml", practice));
                        //    //XDocument doc = XDocument.Load(practiceFileName);
                        //    foreach (string line in lines.Skip(1)) // skip the "head"
                        //    {
                        //        try // split line into fields
                        //        {
                        //            string[] fields = line.Split(',');
                        //            //if (string.Equals(practice, fields[2].Trim(), StringComparison.OrdinalIgnoreCase))
                        //            //{
                        //            string practiceFileName = Path.Combine(dir4practices, string.Format("{0}.xml", fields[2].Trim()));
                        //            string unknownFileName = Path.Combine(dir4practices, "unknown.xml");
                        //            string file2save;
                        //            if (!File.Exists(practiceFileName)) { file2save = unknownFileName; }
                        //            else { file2save = practiceFileName; }
                        //            XDocument doc = XDocument.Load(file2save);
                        //            doc.Element("drugs").Add(new XElement("drug",
                        //                new XElement("sha", fields[0].Trim()),
                        //                new XElement("pct", fields[1].Trim()),
                        //                new XElement("practice_id", fields[2].Trim()),
                        //                new XElement("bnf_code", fields[3].Trim()),
                        //                new XElement("bnf_name", fields[4].Trim()),
                        //                new XElement("items", fields[5].Trim()),
                        //                new XElement("nic", fields[6].Trim()),
                        //                new XElement("act_cost", fields[7].Trim()),
                        //                new XElement("period", fields[8].Trim())
                        //                ));
                        //            doc.Save(file2save);
                        //            //}
                        //        }
                        //        catch (Exception ex)
                        //        {
                        //            Console.WriteLine(string.Format("{0} - error splitting the line. {1}", DateTime.Now.ToString("HH:mm:ss"), ex.Message));
                        //            continue;
                        //        }
                        //    }
                        //    //doc.Save(practiceFileName);
                        //}
                        //Console.WriteLine(string.Format("{0} - The PDP file has been successfully read.", DateTime.Now.ToString("HH:mm:ss")));
                        #endregion
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(string.Format("{0} - error reading the PDP file. {1}", DateTime.Now.ToString("HH:mm:ss"), ex.Message));
                        return 5;
                    }
                }
                else // check if files actually exist
                {
                    if (!File.Exists(addFilePath) || !File.Exists(pdpFilePath))
                    {
                        Console.WriteLine("\nDownloading was skipped, but files do not exist!");
                        return 4;
                    }
                    Console.WriteLine();
                }

                Console.WriteLine(string.Format("\n{0} - Got and parsed the files.\n", DateTime.Now.ToString("HH:mm:ss")));

                //
                // --- doing some research
                //

                Console.WriteLine("Answering the questions\n-----------------------\n");
                DirectoryInfo directory = new DirectoryInfo(dir4pdps);

                // --- 1) counting practices based on ADD file
                Console.WriteLine(string.Format("1) {0}...", DateTime.Now.ToString("HH:mm:ss")));
                XDocument docADD = XDocument.Load(practicesFilePath);
                HashSet<string> practicesADD = new HashSet<string>(
                    (from element in docADD.Root.Elements()
                     select element.Element("id").Value).ToArray()
                     );
                Console.WriteLine(string.Format("Practices count from ADD file: {0}\n...", practicesADD.Count.ToString()));
                // counting practices based on PDP file (cause it contains some unmentioned in ADD)
                HashSet<string> practicesPDD = new HashSet<string>();
                foreach (FileInfo file in directory.GetFiles())
                {
                    if (file.Name.StartsWith("drugs"))
                    {
                        XDocument docPDP = XDocument.Load(file.FullName);
                        var findedElements = from element in docPDP.Root.Elements()
                                             select element.Element("practice_id").Value;
                        foreach (var element in findedElements)
                        {
                            practicesPDD.Add(element);
                        }
                    }
                }
                HashSet<string> diff = new HashSet<string>(practicesPDD.Except(practicesADD));
                Console.WriteLine(string.Format("Practices count from PDD file not mentioned in ADD: {0}", diff.Count()));
                Console.WriteLine(string.Format("So, overall count of practices is: {0}", practicesADD.Count + diff.Count));

                // --- 2) average actual cost of all peppermint oil prescriptions
                Console.WriteLine(string.Format("\n2) {0}...", DateTime.Now.ToString("HH:mm:ss")));
                List<decimal> peppermintCosts = new List<decimal>();
                foreach (FileInfo file in directory.GetFiles())
                {
                    if (file.Name.StartsWith("drugs"))
                    {
                        XDocument docAVG = XDocument.Load(file.FullName);
                        var findedElements = from element in docAVG.Root.Elements()
                                             where string.Equals(element.Element("bnf_name").Value, "Peppermint Oil", StringComparison.OrdinalIgnoreCase)
                                             select new { items = element.Element("items").Value, cost = element.Element("act_cost").Value };
                        foreach (var element in findedElements)
                        {
                            try
                            {
                                // I guess, cost for 1 item is "act_cost" / "items"
                                peppermintCosts.Add(decimal.Parse(element.cost, CultureInfo.InvariantCulture) / decimal.Parse(element.items));
                            }
                            catch { continue; }
                        }
                    }
                }
                Console.WriteLine(string.Format("The average actual cost of all Peppermint Oil prescriptions: {0} per item", peppermintCosts.Average().ToString("#.##")));

                // --- 3) top 5 post codes have the highest actual spend, and how much did each spend in total
                Console.WriteLine(string.Format("\n3) {0}...", DateTime.Now.ToString("HH:mm:ss")));
                Dictionary<string, decimal> postCodes = new Dictionary<string, decimal>();
                XDocument docPractices = XDocument.Load(practicesFilePath);
                foreach (FileInfo file in directory.GetFiles())
                {
                    if (file.Name.StartsWith("drugs"))
                    {
                        XDocument docDrugs = XDocument.Load(file.FullName);
                        var findedElements = from drug in docDrugs.Root.Elements()
                                             join practice in docPractices.Root.Elements() on drug.Element("practice_id").Value equals practice.Element("id").Value
                                             select new { code = practice.Element("postcode").Value, cost = drug.Element("act_cost").Value };
                        foreach (var element in findedElements)
                        {
                            decimal cost = decimal.Parse(element.cost, CultureInfo.InvariantCulture);
                            if (!postCodes.ContainsKey(element.code))
                            {
                                postCodes.Add(element.code, cost);
                            }
                            else
                            {
                                postCodes[element.code] = postCodes[element.code] + cost;
                            }
                        }
                    }
                }
                var postCodesTop5 = postCodes.OrderByDescending(x => x.Value).Take(5);
                Console.WriteLine(string.Format("Top 5 postcodes with their costs (among those whos IDs are presented in ADD file):"));
                foreach (var postcode in postCodesTop5)
                {
                    Console.WriteLine(string.Format("- practices from \"{0}\" spent {1} in total", postcode.Key, postcode.Value));
                }

                // --- 4) average price per prescription of Flucloxacillin (excluding Co-Fluampicil)
                Console.WriteLine(string.Format("\n4) {0}...", DateTime.Now.ToString("HH:mm:ss")));
                List<KeyValuePair<string, decimal>> costsByRegions = new List<KeyValuePair<string, decimal>>();
                XDocument docPracticesAVG = XDocument.Load(practicesFilePath);
                foreach (FileInfo file in directory.GetFiles())
                {
                    if (file.Name.StartsWith("drugs"))
                    {
                        XDocument docDrugs = XDocument.Load(file.FullName);
                        var findedElements = from drug in docDrugs.Root.Elements()
                                             join practice in docPracticesAVG.Root.Elements() on drug.Element("practice_id").Value equals practice.Element("id").Value
                                             where containsIgnoreCase(drug.Element("bnf_name").Value, "Flucloxacillin") && !containsIgnoreCase(drug.Element("bnf_name").Value, "Co-Fluampicil")
                                             select new { code = practice.Element("region").Value, cost = drug.Element("act_cost").Value, items = drug.Element("items").Value };
                        foreach (var element in findedElements)
                        {
                            costsByRegions.Add(
                                new KeyValuePair<string, decimal>(
                                    element.code,
                                    decimal.Parse(element.cost, CultureInfo.InvariantCulture) / decimal.Parse(element.items)
                                    ));
                        }
                    }
                }
                var grp = (from c in costsByRegions
                           group c by c.Key into g
                           select new KeyValuePair<string, decimal>(g.Key, g.Average(x => x.Value))).ToList();
                // national mean, I guess
                decimal averageNationalPrice = grp.Average(x => x.Value);
                Console.WriteLine(string.Format("Average national mean: {0}", averageNationalPrice.ToString("#.##")));
                // sorting by avg value
                var grpOrdered = grp.OrderByDescending(x => x.Value);
                string avgResults = Path.Combine(dirNameXML, "avg-price.txt");
                // delete previous file
                if (File.Exists(avgResults)) { File.Delete(avgResults); }
                using (StreamWriter writetext = File.AppendText(avgResults))
                {
                    foreach (var g in grpOrdered)
                    {
                        writetext.WriteLine(string.Format("{0}: {1} - it's {2}% from national mean", g.Key, g.Value.ToString("#.##"), (g.Value / averageNationalPrice * 100).ToString("#.##")));
                    }
                }
                Console.WriteLine(string.Format("Averege price by regions results have been saved to the file {0}", avgResults));

                // --- 5) show top 10 practices (ID and name) with smallest volume of items and compare it with average among all
                Console.WriteLine(string.Format("\n5) {0}...", DateTime.Now.ToString("HH:mm:ss")));
                List<KeyValuePair<string, int>> itemsByPractice = new List<KeyValuePair<string, int>>();
                XDocument docPracticesMinItems = XDocument.Load(practicesFilePath);
                foreach (FileInfo file in directory.GetFiles())
                {
                    if (file.Name.StartsWith("drugs"))
                    {
                        XDocument docDrugs = XDocument.Load(file.FullName);
                        var findedElements = from drug in docDrugs.Root.Elements()
                                             join practice in docPracticesMinItems.Root.Elements() on drug.Element("practice_id").Value equals practice.Element("id").Value
                                             select new { id = string.Format("[{0}] {1}", practice.Element("id").Value, practice.Element("name").Value), items = drug.Element("items").Value };
                        foreach (var element in findedElements)
                        {
                            itemsByPractice.Add(
                                new KeyValuePair<string, int>(
                                    element.id,
                                    int.Parse(element.items)
                                    ));
                        }
                    }
                }
                var grpItems = (from c in itemsByPractice
                                group c by c.Key into g
                                select new KeyValuePair<string, int>(g.Key, g.Sum(x => x.Value))).ToList();
                int averageItems = (int)grpItems.Average(x => x.Value);
                Console.WriteLine(string.Format("Overall average items of prescriptions: {0}", averageItems));
                // sorting
                var grpItemsOrdered = grpItems.OrderBy(x => x.Value).Take(10);
                Console.WriteLine("The following practice for some reasons have the lowest number of prescriptions:");
                foreach (var g in grpItemsOrdered)
                {
                    Console.WriteLine(string.Format("- {0}: {1}, it's {2}% from average value", g.Key, g.Value, g.Value / averageItems));
                }

                //
                // --- TEH END
                //

                Console.WriteLine(string.Format("\n-----------\n{0} - The application has finished its work.", DateTime.Now.ToString("HH:mm:ss")));

                // everything is okay
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("\nUnknown error! {0}\n", ex.Message));
                return 1;
            }
        }

        public static void downloadTheFile(string link2file, string path2save)
        {
            HttpWebRequest httpRequest = (HttpWebRequest)WebRequest.Create(link2file);
            httpRequest.Method = WebRequestMethods.Http.Get;
            HttpWebResponse httpResponse = (HttpWebResponse)httpRequest.GetResponse();

            using (Stream httpResponseStream = httpResponse.GetResponseStream())
            using (FileStream fileStream = File.Create(path2save))
            {
                int bufferSize = 1024;
                byte[] buffer = new byte[bufferSize];
                int bytesRead = 0;

                while ((bytesRead = httpResponseStream.Read(buffer, 0, bufferSize)) != 0)
                {
                    fileStream.Write(buffer, 0, bytesRead);
                }
                fileStream.Close();
                httpResponseStream.Close();
            }
        }

        /// <summary>
        /// if string contains a substring (ignoring case)
        /// </summary>
        /// <param name="source">source string</param>
        /// <param name="toCheck">substring</param>
        /// <returns>
        /// true - contains
        /// false - does not contain
        /// </returns>
        public static bool containsIgnoreCase(string source, string toCheck)
        {
            return source.IndexOf(toCheck, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
