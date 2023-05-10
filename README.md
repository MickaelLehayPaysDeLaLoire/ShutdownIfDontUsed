Projet Visual Studio 2022 écrit en C# utilisant le Framework .NET 6.0 et le package NuGet « Microsoft.Extensions.Hosting.WindowsServices »

# ShutdownIfDontUsed
Service Windows nommé ShutDownService :
Ce service compte le temps pendant lequel personne n'est connecté à l'ordinateur, au bout d'un certain temps, il affiche le programme [DisplayTimeOut](https://github.com/MickaelLehayPaysDeLaLoire/DisplayTimeOut) devant la fenêtre de Login, un compte à rebours avant arrêt du PC s'affiche (Si un utilisateur arrive durant ce temps, il est possible d'annuler cet arrêt programmé en cliquant sur un bouton dédié.)

Installation : 
Cliquer sur install.bat 
cela copie les fichiers nécessaires dans le répertoire C:\Shutdown\ et installe le service Windows.

Dépendance nécessaire au fonctionnement :
https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-6.0.16-windows-x64-installer
