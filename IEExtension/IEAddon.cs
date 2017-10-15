using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using mshtml;
using SHDocVw;
using System.Net.Http;
using System.Net.Http.Headers;

namespace IEExtension
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    [Guid("D40C654D-7C51-4EB3-95B2-1E23905C2A2D")]
    [ProgId("MyBHO.WordHighlighter")]
    public class WordHighlighterBHO : IObjectWithSite
    {
        const string DefaultTextToHighlight = "browser";
        const string ConnectUrl = "https://qa-capture.zephyr4jiracloud.com/capture";
        IWebBrowser2 browser;
        private object site;
        static string url = ConnectUrl + "/session/user/active/link?userName={0}&baseUrl={1}";


        #region Highlight Text
        void OnDocumentComplete(object pDisp, ref object URL)
        {
            try
            {
                // @Eric Stob: Thanks for this hint!
                // This will prevent this method being executed more than once.
                if (pDisp != this.site)
                    return;

                var document2 = browser.Document as IHTMLDocument2;

                var link = browser.LocationURL;
                Uri uri = new Uri(link);
                String[] hostParts = uri.Host.Split('.');
                if (hostParts.Length > 2 && hostParts[1].Equals("atlassian") &&
                    browser.Document != null && browser.Document.GetElementById("header-details-user-fullname") != null &&
                    browser.Document.GetElementById("header-details-user-fullname").GetAttribute("data-username") != null
                    ) //start atlassian.net checking
                {
                    string activeSessionLink = String.Empty;
                    string username = String.Empty;
                    username = browser.Document.GetElementById("header-details-user-fullname").GetAttribute("data-username");
                    // username = "masud.java"; //browser.Document.GetElementById("header-details-user-fullname").GetAttribute("data-username");
                    string jiraUrl = uri.Scheme + "://" + uri.Host;


                    if (username != null)
                    {
                        HttpClient client = new HttpClient();
                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                        HttpResponseMessage response = client.GetAsync(string.Format(url, username, jiraUrl)).Result;  // Blocking call!  
                        if (response.IsSuccessStatusCode)
                        {
                            activeSessionLink = response.Content.ReadAsStringAsync().Result;
                        }
                        else
                        {
                            Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
                        }


                        var element = document2.createElement("div");
                        element.innerHTML = activeSessionLink;
                        element.setAttribute("id", "myDiv", 0);
                        element.setAttribute("class", "myDivClass", 0);
                        document2.body.insertAdjacentHTML("afterBegin", element.outerHTML);


                    } //end atlassian.net checking

                }


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                //MessageBox.Show(ex.Message);
            }
        }
        #endregion
        #region Load and Save Data
        static string TextToHighlight = DefaultTextToHighlight;
        public static string RegData = "Software\\MyIEExtension";

        public class Session
        {
            public string Id { get; set; }
            public string Name { get; set; }
        }

        [DllImport("ieframe.dll")]
        public static extern int IEGetWriteableHKCU(ref IntPtr phKey);

        private static void SaveOptions()
        {
            // In IE 7,8,9,(desktop)10 tabs run in Protected Mode
            // which prohibits writes to HKLM, HKCU.
            // Must ask IE for "Writable" registry section pointer
            // which will be something like HKU/S-1-7***/Software/AppDataLow/
            // In "metro" IE 10 mode, tabs run in "Enhanced Protected Mode"
            // where BHOs are not allowed to run, except in edge cases.
            IntPtr phKey = new IntPtr();
            var answer = IEGetWriteableHKCU(ref phKey);
            RegistryKey writeable_registry = RegistryKey.FromHandle(
                new Microsoft.Win32.SafeHandles.SafeRegistryHandle(phKey, true)
            );
            RegistryKey registryKey = writeable_registry.OpenSubKey(RegData, true);

            if (registryKey == null)
                registryKey = writeable_registry.CreateSubKey(RegData);
            registryKey.SetValue("Data", TextToHighlight);

            writeable_registry.Close();
        }
        private static void LoadOptions()
        {
            // In IE 7,8,9,(desktop)10 tabs run in Protected Mode
            // which prohibits writes to HKLM, HKCU.
            // Must ask IE for "Writable" registry section pointer
            // which will be something like HKU/S-1-7***/Software/AppDataLow/
            // In "metro" IE 10 mode, tabs run in "Enhanced Protected Mode"
            // where BHOs are not allowed to run, except in edge cases.
            IntPtr phKey = new IntPtr();
            var answer = IEGetWriteableHKCU(ref phKey);
            RegistryKey writeable_registry = RegistryKey.FromHandle(
                new Microsoft.Win32.SafeHandles.SafeRegistryHandle(phKey, true)
            );
            RegistryKey registryKey = writeable_registry.OpenSubKey(RegData, true);

            if (registryKey == null)
                registryKey = writeable_registry.CreateSubKey(RegData);
            registryKey.SetValue("Data", TextToHighlight);

            if (registryKey == null)
            {
                TextToHighlight = DefaultTextToHighlight;
            }
            else
            {
                TextToHighlight = (string)registryKey.GetValue("Data");
            }
            writeable_registry.Close();
        }
        #endregion

        [Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
        [InterfaceType(1)]
        public interface IServiceProvider
        {
            int QueryService(ref Guid guidService, ref Guid riid, out IntPtr ppvObject);
        }

        #region Implementation of IObjectWithSite
        int IObjectWithSite.SetSite(object site)
        {
            this.site = site;

            if (site != null)
            {
                LoadOptions();

                var serviceProv = (IServiceProvider)this.site;
                var guidIWebBrowserApp = Marshal.GenerateGuidForType(typeof(IWebBrowserApp)); // new Guid("0002DF05-0000-0000-C000-000000000046");
                var guidIWebBrowser2 = Marshal.GenerateGuidForType(typeof(IWebBrowser2)); // new Guid("D30C1661-CDAF-11D0-8A3E-00C04FC9E26E");
                IntPtr intPtr;
                serviceProv.QueryService(ref guidIWebBrowserApp, ref guidIWebBrowser2, out intPtr);

                browser = (IWebBrowser2)Marshal.GetObjectForIUnknown(intPtr);

                ((DWebBrowserEvents2_Event)browser).DocumentComplete +=
                    new DWebBrowserEvents2_DocumentCompleteEventHandler(this.OnDocumentComplete);
            }
            else
            {
                ((DWebBrowserEvents2_Event)browser).DocumentComplete -=
                    new DWebBrowserEvents2_DocumentCompleteEventHandler(this.OnDocumentComplete);
                browser = null;
            }
            return 0;
        }
        int IObjectWithSite.GetSite(ref Guid guid, out IntPtr ppvSite)
        {
            IntPtr punk = Marshal.GetIUnknownForObject(browser);
            int hr = Marshal.QueryInterface(punk, ref guid, out ppvSite);
            Marshal.Release(punk);
            return hr;
        }
        #endregion


        #region Registering with regasm
        public static string RegBHO = "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Browser Helper Objects";
        public static string RegCmd = "Software\\Microsoft\\Internet Explorer\\Extensions";

        [ComRegisterFunction]
        public static void RegisterBHO(Type type)
        {
            string guid = type.GUID.ToString("B");

            // BHO
            {
                RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(RegBHO, true);
                if (registryKey == null)
                    registryKey = Registry.LocalMachine.CreateSubKey(RegBHO);
                RegistryKey key = registryKey.OpenSubKey(guid);
                if (key == null)
                    key = registryKey.CreateSubKey(guid);
                key.SetValue("Alright", 1);
                registryKey.Close();
                key.Close();
            }


        }

        [ComUnregisterFunction]
        public static void UnregisterBHO(Type type)
        {
            string guid = type.GUID.ToString("B");
            // BHO
            {
                RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(RegBHO, true);
                if (registryKey != null)
                    registryKey.DeleteSubKey(guid, false);
            }

        }
        #endregion

    }
}