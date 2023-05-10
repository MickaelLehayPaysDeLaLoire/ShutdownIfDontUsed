using ShutdownIfDontUsed.Services.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Timers;
using Timer = System.Timers.Timer;
using System.IO.Pipes;
using System.Xml;
using System.Net.Mime;
using System;
using System.Runtime.InteropServices;

namespace ShutdownIfDontUsed
{
    /// <summary>
    /// The main Worker Service.
    /// </summary>
    public class Worker : BackgroundService
    {
        // importation de fonctions systemes via la DLL Wtsapi32.dll
        [DllImport("Wtsapi32.dll")]
        private static extern bool WTSQuerySessionInformation(IntPtr hServer, int sessionId, WtsInfoClass wtsInfoClass, out IntPtr ppBuffer, out int pBytesReturned);
        [DllImport("Wtsapi32.dll")]
        private static extern void WTSFreeMemory(IntPtr pointer);
        private enum WtsInfoClass
        {
            WTSUserName = 5,
            WTSDomainName = 7,
        }        
        
        private bool b_Logon=false;       // flag servant � savoir si une connection utilisateur a �t� d�tect�e
        private bool b_shutdown = false;  // flag servant � savoir si un arret est d�j� en cours.
        private int iTempsPasse = 15;     // valeur en seconde, s'incrementant tant que personne n'est connect�  
        private int iTempMin = 10;        // valeur minimun en minutes afin d'�viter qu'un malin mette 0 et que cela empeche de laisser le temps de se connecter
        private int itempLimite = 1;      // valeur en minutes r�presentant � partir de combien de temps la programme d'arr�t (interface graphique) sera lanc�
        private int icancelTimer = 40;    // valeur en secondes representant le temps de possibilit� d'annulation de l'arr�t (cette valeur sera transmise via arguments au programme d'arr�t )
        private string userName;          // nom de l'utilisateur connect� s'il y en a un.
        private Timer timer;                

        #region Readonlys

        private readonly ILogger<Worker> _logger;
        private readonly IProcessServices _processServices;

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="logger"><see cref="ILogger"/></param>
        /// <param name="processServices"><see cref="IProcessServices"/></param>
        public Worker(ILogger<Worker> logger, IProcessServices processServices)
        {
            _logger = logger;
            _processServices = processServices;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Executes when the service has started.
        /// </summary>
        /// <param name="stoppingToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("** SERVICE STARTED **");
                EventLog logListener = new EventLog("Security");
                logListener.EntryWritten += logListener_EntryWritten; // fonction appel�e quand le journal d'�venement "Securit�" recois une entr�e
                logListener.EnableRaisingEvents = true;
                Process userProcess = null;
                iTempsPasse = 0;
                b_shutdown = false;
                // Set up a timer that triggers every minute.
                timer = new System.Timers.Timer();
                timer.Interval = 60000; // 60000 = 1 minutes
                timer.Elapsed += new ElapsedEventHandler(this.OnTimer);
                timer.Start();
                TestIfUserIsConnected();
                XmlDocument xmlDoc = new XmlDocument();
                try
                {
                    xmlDoc.Load(@"C:\Shutdown\ShutdownIfDontUsed.xml");
                    XmlNodeList? xmlNodeList = xmlDoc.DocumentElement.SelectNodes("/Options");
                    foreach (XmlNode xmlNode in xmlNodeList)
                    {
                        int.TryParse(xmlNode.SelectSingleNode("ShutdownTimer").InnerText, out itempLimite);
                        if (itempLimite< iTempMin)
                        {
                            itempLimite = iTempMin;  // minimun de securit�
                        }
                        _logger.LogInformation("ShutdownTimer : " + itempLimite);
                        int.TryParse(xmlNode.SelectSingleNode("CancelTimer").InnerText, out icancelTimer);
                        _logger.LogInformation("CancelTimer : " + icancelTimer);
                    }
                }
                catch (Exception ex)
                {   // valeurs par defaut
                    _logger.LogInformation("ShutdownTimer : " + itempLimite);
                    _logger.LogInformation("CancelTimer : " + icancelTimer);
                }
                if (!stoppingToken.IsCancellationRequested)
                {

                }
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Executes when the service is ready to start.
        /// </summary>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting service");
            return base.StartAsync(cancellationToken);
        }

        private void TestIfUserIsConnected()
        {
            Process[] processes = Process.GetProcessesByName("winlogon");
            if (processes.Any())
            {
                int userProcess = processes[0].SessionId;                
                userName = GetUsername(userProcess);
                // System.IO.File.AppendAllLines(@"c:\Shutdown\log.txt", new string[] { "l'id correspond au user : " + userName });
            }
            if (userName == "SYSTEM")
            {
                b_Logon = false;
            }
            else  // un utilisateur s'est connect�
            {
                b_Logon = true;
                iTempsPasse = 0; // remise � z�ro du compteur
            }
        }

        /// <summary>
        /// Executes when the service is performing a graceful shutdown.
        /// </summary>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping service");
            return base.StopAsync(cancellationToken);
        }

        public void OnTimer(object sender, ElapsedEventArgs args)
        {
            if (b_Logon == false) //tant que personne est connect�
            {
                iTempsPasse++;    // on incr�mente 
            }
            else                  // quelqu'un c'est connect�
            {
                iTempsPasse = 0;  // remise � z�ro du compteur
            }
            if (b_shutdown == false) // Si nous ne somme pas d�j� en train d'afficher le programme d'arret
            {                        // nous pouvons tester si nous n'avons pas d�pass� la dur�e limite
                if (iTempsPasse >= itempLimite)
                {
                    DateTime thisDay = DateTime.Today;
                    System.IO.File.AppendAllLines(@"c:\Shutdown\log.txt", new string[] { thisDay.ToString()+" : Temps d'inactivit� atteint " + iTempsPasse + " minute(s)" });
                    b_shutdown = true; // on passe le flag � true pour �viter des tests maintenant inutiles.
                    timer.Stop(); // on arrete le Timer
                    Process[] processes = Process.GetProcessesByName("winlogon"); // on recupere le(s) process Winlogon
                    if (processes.Any()) 
                    {
                        Process winlogonProcess = processes[0];
                        // on recupere le premier process Winlogon
                        // afin de lancer le programme d'arret avec les m�mes droits et attributs
                        // sinon celui-ci ne pourrait pas �tre visible l'�cran de connection.
                        // on transmet dans la ligne de commande les arguments les valeurs temps limite et temps d'annulation possible au programme d'arret
                        _processServices.StartProcessAsCurrentUser(@"c:\Shutdown\DisplayTimeOut\DisplayTimeOut.exe " + itempLimite.ToString() + " " + icancelTimer.ToString(), null, winlogonProcess);
                    }
                }
            }
        }
        public void logListener_EntryWritten(object sender, EntryWrittenEventArgs e)
        {
            // fonction appel�e quand le journal d'�venement "Securit�" recois une entr�e
            // on test les Id d'entr�e suivant :
                                                // 4624: An account was successfully logged on.                                                
                                                // 4634: An account was successfully logged off.                                                
                                                
            if ((e.Entry.EventID == 4624) || (e.Entry.EventID == 5379))
            {
                // logon ??
                TestIfUserIsConnected();
            }

            if ((e.Entry.EventID == 4647) || (e.Entry.EventID == 4634))
            {
                // logoff ??
                TestIfUserIsConnected();                
            }
        }    
        
        private static string GetUsername(int sessionId, bool prependDomain = true)
        {
            IntPtr buffer;
            int strLen;
            string username = "SYSTEM";
            if (WTSQuerySessionInformation(IntPtr.Zero, sessionId, WtsInfoClass.WTSUserName, out buffer, out strLen) && strLen > 1)
            {
                username = Marshal.PtrToStringAnsi(buffer);
                WTSFreeMemory(buffer);
                if (prependDomain)
                {
                    if (WTSQuerySessionInformation(IntPtr.Zero, sessionId, WtsInfoClass.WTSDomainName, out buffer, out strLen) && strLen > 1)
                    {
                        username = Marshal.PtrToStringAnsi(buffer) + "\\" + username;
                        WTSFreeMemory(buffer);
                    }
                }
            }
            return username;
        }        
    }

    #endregion

}