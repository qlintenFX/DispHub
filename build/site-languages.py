# Writes the home page in other languages (site/<lang>/index.html) from the English one, and links every version to
# the others (hreflang for search engines, a row of language names in the footer). Run it from the repo root after
# changing site/index.html, and add a line below for any new or changed English text:
#   python build/site-languages.py
# Strings that aren't translated stay in English, so the script never breaks a page.
import json
import pathlib
import re

SITE = pathlib.Path("site")
HOME = "https://getdisphub.com/"
LANGS = {  # folder: (html lang, name in its own language)
    "": ("en-GB", "English"),
    "de": ("de", "Deutsch"),
    "es": ("es", "Español"),
    "fr": ("fr", "Français"),
    "it": ("it", "Italiano"),
    "nl": ("nl", "Nederlands"),
    "pt-br": ("pt-BR", "Português"),
}

T = {
"DispHub: display colour profiles and vibrance for Windows": {
    "de": "DispHub: Bildschirm-Farbprofile und Vibrance für Windows",
    "es": "DispHub: perfiles de color de pantalla y vibrance para Windows",
    "fr": "DispHub : profils de couleur d'écran et vibrance pour Windows",
    "it": "DispHub: profili colore dello schermo e vibrance per Windows",
    "nl": "DispHub: kleurprofielen en vibrance voor je scherm op Windows",
    "pt-br": "DispHub: perfis de cor da tela e vibrance para Windows"},
"Save vibrance, gamma, contrast, brightness and colour temperature as profiles and switch them with a hotkey, per app or on a schedule. Free for Windows 10 and 11.": {
    "de": "Speichere Farbintensität, Gamma, Kontrast, Helligkeit und Farbtemperatur als Profile und wechsle per Tastenkombination, pro App oder nach Zeitplan. Kostenlos für Windows 10 und 11.",
    "es": "Guarda intensidad de color, gamma, contraste, brillo y temperatura de color como perfiles y cámbialos con un atajo, por aplicación o con un horario. Gratis para Windows 10 y 11.",
    "fr": "Enregistrez vibrance, gamma, contraste, luminosité et température de couleur en profils et changez-en par raccourci, par application ou selon un horaire. Gratuit pour Windows 10 et 11.",
    "it": "Salva vivacità, gamma, contrasto, luminosità e temperatura colore come profili e cambiali con una scorciatoia, per app o con una programmazione. Gratis per Windows 10 e 11.",
    "nl": "Sla kleurintensiteit, gamma, contrast, helderheid en kleurtemperatuur op als profielen en wissel met een sneltoets, per app of volgens een schema. Gratis voor Windows 10 en 11.",
    "pt-br": "Salve vibrance, gama, contraste, brilho e temperatura de cor como perfis e troque com um atalho, por app ou com um agendamento. Grátis para Windows 10 e 11."},
"Switch in a click, by hotkey, per app or on a schedule. Free for Windows 10 and 11.": {
    "de": "Wechsle per Klick, Tastenkombination, pro App oder nach Zeitplan. Kostenlos für Windows 10 und 11.",
    "es": "Cambia con un clic, con un atajo, por aplicación o con un horario. Gratis para Windows 10 y 11.",
    "fr": "Changez en un clic, par raccourci, par application ou selon un horaire. Gratuit pour Windows 10 et 11.",
    "it": "Cambia con un clic, con una scorciatoia, per app o con una programmazione. Gratis per Windows 10 e 11.",
    "nl": "Wissel met één klik, een sneltoets, per app of volgens een schema. Gratis voor Windows 10 en 11.",
    "pt-br": "Troque com um clique, um atalho, por app ou com um agendamento. Grátis para Windows 10 e 11."},
"Colour profiles for your display: brightness, contrast, gamma, colour temperature and vibrance, switched by hotkey, per app or on a schedule.": {
    "de": "Farbprofile für deinen Bildschirm: Helligkeit, Kontrast, Gamma, Farbtemperatur und Farbintensität, gewechselt per Tastenkombination, pro App oder nach Zeitplan.",
    "es": "Perfiles de color para tu pantalla: brillo, contraste, gamma, temperatura de color e intensidad, cambiados con un atajo, por aplicación o con un horario.",
    "fr": "Des profils de couleur pour votre écran : luminosité, contraste, gamma, température de couleur et vibrance, changés par raccourci, par application ou selon un horaire.",
    "it": "Profili colore per il tuo schermo: luminosità, contrasto, gamma, temperatura colore e vivacità, cambiati con una scorciatoia, per app o con una programmazione.",
    "nl": "Kleurprofielen voor je scherm: helderheid, contrast, gamma, kleurtemperatuur en kleurintensiteit, gewisseld met een sneltoets, per app of volgens een schema.",
    "pt-br": "Perfis de cor para a sua tela: brilho, contraste, gama, temperatura de cor e vibrance, trocados com um atalho, por app ou com um agendamento."},
">Skip to content<": {"de": ">Zum Inhalt springen<", "es": ">Ir al contenido<", "fr": ">Aller au contenu<", "it": ">Vai al contenuto<", "nl": ">Naar de inhoud<", "pt-br": ">Ir para o conteúdo<"},
">Features<": {"de": ">Funktionen<", "es": ">Funciones<", "fr": ">Fonctionnalités<", "it": ">Funzionalità<", "nl": ">Functies<", "pt-br": ">Recursos<"},
'aria-label="Features"': {"de": 'aria-label="Funktionen"', "es": 'aria-label="Funciones"', "fr": 'aria-label="Fonctionnalités"', "it": 'aria-label="Funzionalità"', "nl": 'aria-label="Functies"', "pt-br": 'aria-label="Recursos"'},
">Download<": {"de": ">Download<", "es": ">Descargar<", "fr": ">Télécharger<", "it": ">Scarica<", "nl": ">Downloaden<", "pt-br": ">Baixar<"},
'aria-label="Menu"': {"de": 'aria-label="Menü"', "es": 'aria-label="Menú"', "fr": 'aria-label="Menu"', "it": 'aria-label="Menu"', "nl": 'aria-label="Menu"', "pt-br": 'aria-label="Menu"'},
"Colour profiles for your display.": {
    "de": "Farbprofile für deinen Bildschirm.", "es": "Perfiles de color para tu pantalla.", "fr": "Des profils de couleur pour votre écran.",
    "it": "Profili colore per il tuo schermo.", "nl": "Kleurprofielen voor je scherm.", "pt-br": "Perfis de cor para a sua tela."},
"Switch in a click, by hotkey, per app or on a schedule.": {
    "de": "Wechsle per Klick, Tastenkombination, pro App oder nach Zeitplan.", "es": "Cambia con un clic, con un atajo, por aplicación o con un horario.",
    "fr": "Changez en un clic, par raccourci, par application ou selon un horaire.", "it": "Cambia con un clic, con una scorciatoia, per app o con una programmazione.",
    "nl": "Wissel met één klik, een sneltoets, per app of volgens een schema.", "pt-br": "Troque com um clique, um atalho, por app ou com um agendamento."},
'alt="Download from the Microsoft Store"': {
    "de": 'alt="Im Microsoft Store herunterladen"', "es": 'alt="Descargar en Microsoft Store"', "fr": 'alt="Télécharger dans le Microsoft Store"',
    "it": 'alt="Scarica da Microsoft Store"', "nl": 'alt="Downloaden in de Microsoft Store"', "pt-br": 'alt="Baixar na Microsoft Store"'},
"View on GitHub<small>Open source</small>": {
    "de": "Auf GitHub ansehen<small>Open Source</small>", "es": "Ver en GitHub<small>Código abierto</small>", "fr": "Voir sur GitHub<small>Open source</small>",
    "it": "Vedi su GitHub<small>Open source</small>", "nl": "Bekijk op GitHub<small>Open source</small>", "pt-br": "Ver no GitHub<small>Código aberto</small>"},
">Install from Terminal<": {"de": ">Im Terminal installieren<", "es": ">Instalar desde Terminal<", "fr": ">Installer depuis le Terminal<", "it": ">Installa dal Terminale<", "nl": ">Installeren via Terminal<", "pt-br": ">Instalar pelo Terminal<"},
'data-copied="Copied. Paste it into Terminal:"': {
    "de": 'data-copied="Kopiert. Füge es im Terminal ein:"', "es": 'data-copied="Copiado. Pégalo en Terminal:"', "fr": 'data-copied="Copié. Collez-le dans le Terminal :"',
    "it": 'data-copied="Copiato. Incollalo nel Terminale:"', "nl": 'data-copied="Gekopieerd. Plak het in Terminal:"', "pt-br": 'data-copied="Copiado. Cole no Terminal:"'},
'data-run="Run this in Terminal:"': {
    "de": 'data-run="Führe das im Terminal aus:"', "es": 'data-run="Ejecuta esto en Terminal:"', "fr": 'data-run="Exécutez ceci dans le Terminal :"',
    "it": 'data-run="Esegui questo nel Terminale:"', "nl": 'data-run="Voer dit uit in Terminal:"', "pt-br": 'data-run="Execute isto no Terminal:"'},
'class="eyebrow">Profiles<': {"de": 'class="eyebrow">Profile<', "es": 'class="eyebrow">Perfiles<', "fr": 'class="eyebrow">Profils<', "it": 'class="eyebrow">Profili<', "nl": 'class="eyebrow">Profielen<', "pt-br": 'class="eyebrow">Perfis<'},
'class="eyebrow">Hotkeys<': {"de": 'class="eyebrow">Tastenkombinationen<', "es": 'class="eyebrow">Atajos<', "fr": 'class="eyebrow">Raccourcis<', "it": 'class="eyebrow">Scorciatoie<', "nl": 'class="eyebrow">Sneltoetsen<', "pt-br": 'class="eyebrow">Atalhos<'},
'class="eyebrow">Apps<': {"de": 'class="eyebrow">Apps<', "es": 'class="eyebrow">Aplicaciones<', "fr": 'class="eyebrow">Applications<', "it": 'class="eyebrow">App<', "nl": 'class="eyebrow">Apps<', "pt-br": 'class="eyebrow">Apps<'},
'class="eyebrow">Schedules<': {"de": 'class="eyebrow">Zeitpläne<', "es": 'class="eyebrow">Horarios<', "fr": 'class="eyebrow">Horaires<', "it": 'class="eyebrow">Programmazioni<', "nl": 'class="eyebrow">Schema\'s<', "pt-br": 'class="eyebrow">Agendamentos<'},
"A look for every moment.": {"de": "Ein Look für jeden Moment.", "es": "Un aspecto para cada momento.", "fr": "Un rendu pour chaque moment.", "it": "Un look per ogni momento.", "nl": "Een look voor elk moment.", "pt-br": "Um visual para cada momento."},
"Brightness, contrast, gamma, colour temperature and vibrance, saved as profiles.": {
    "de": "Helligkeit, Kontrast, Gamma, Farbtemperatur und Farbintensität, als Profile gespeichert.",
    "es": "Brillo, contraste, gamma, temperatura de color e intensidad, guardados como perfiles.",
    "fr": "Luminosité, contraste, gamma, température de couleur et vibrance, enregistrés en profils.",
    "it": "Luminosità, contrasto, gamma, temperatura colore e vivacità, salvati come profili.",
    "nl": "Helderheid, contrast, gamma, kleurtemperatuur en kleurintensiteit, opgeslagen als profielen.",
    "pt-br": "Brilho, contraste, gama, temperatura de cor e vibrance, salvos como perfis."},
"Instantly switch.": {"de": "Sofort wechseln.", "es": "Cambia al instante.", "fr": "Changez instantanément.", "it": "Cambia all'istante.", "nl": "Wissel meteen.", "pt-br": "Troque na hora."},
"Works with any game and any app.": {"de": "Funktioniert mit jedem Spiel und jeder App.", "es": "Funciona con cualquier juego y cualquier aplicación.", "fr": "Fonctionne avec tous les jeux et toutes les applications.", "it": "Funziona con qualsiasi gioco e qualsiasi app.", "nl": "Werkt met elke game en elke app.", "pt-br": "Funciona com qualquer jogo e qualquer app."},
"Every app gets its own look.": {"de": "Jede App bekommt ihren eigenen Look.", "es": "Cada aplicación con su propio aspecto.", "fr": "Chaque application a son propre rendu.", "it": "Ogni app ha il suo look.", "nl": "Elke app krijgt zijn eigen look.", "pt-br": "Cada app com seu próprio visual."},
"Your profile while the app is in front, normal colours when you switch away.": {
    "de": "Dein Profil, solange die App im Vordergrund ist, normale Farben, sobald du wechselst.",
    "es": "Tu perfil mientras la aplicación está al frente y colores normales cuando cambias.",
    "fr": "Votre profil tant que l'application est au premier plan, les couleurs normales quand vous en sortez.",
    "it": "Il tuo profilo mentre l'app è in primo piano, colori normali quando passi ad altro.",
    "nl": "Je profiel zolang de app op de voorgrond staat, normale kleuren zodra je wegschakelt.",
    "pt-br": "Seu perfil enquanto o app está em primeiro plano, cores normais quando você troca."},
"Schedule your profiles.": {"de": "Plane deine Profile.", "es": "Programa tus perfiles.", "fr": "Programmez vos profils.", "it": "Programma i tuoi profili.", "nl": "Plan je profielen.", "pt-br": "Agende seus perfis."},
"Switch at sunset and sunrise, or at the hours you choose.": {
    "de": "Wechsle bei Sonnenuntergang und Sonnenaufgang oder zu den Uhrzeiten, die du wählst.",
    "es": "Cambia al atardecer y al amanecer, o a las horas que elijas.", "fr": "Changez au coucher et au lever du soleil, ou aux heures de votre choix.",
    "it": "Cambia al tramonto e all'alba, o agli orari che scegli.", "nl": "Wissel bij zonsondergang en zonsopkomst, of op de tijden die jij kiest.",
    "pt-br": "Troque ao pôr do sol e ao nascer do sol, ou nos horários que você escolher."},
"DispHub's Profiles page: Standard, Gaming, Night and Film, with Gaming's sliders": {
    "de": "Die Profilseite von DispHub: Standard, Gaming, Nacht und Film, mit den Reglern von Gaming",
    "es": "La página Perfiles de DispHub: Estándar, Gaming, Noche y Cine, con los controles de Gaming",
    "fr": "La page Profils de DispHub : Standard, Jeu, Nuit et Film, avec les curseurs de Jeu",
    "it": "La pagina Profili di DispHub: Standard, Gaming, Notte e Film, con i cursori di Gaming",
    "nl": "De pagina Profielen van DispHub: Standaard, Gaming, Nacht en Film, met de schuifregelaars van Gaming",
    "pt-br": "A página Perfis do DispHub: Padrão, Jogos, Noite e Filme, com os controles de Jogos"},
"A photo with the Gaming profile on": {"de": "Ein Foto mit dem Gaming-Profil", "es": "Una foto con el perfil Gaming activado", "fr": "Une photo avec le profil Jeu activé", "it": "Una foto con il profilo Gaming attivo", "nl": "Een foto met het Gaming-profiel aan", "pt-br": "Uma foto com o perfil Jogos ativado"},
"DispHub's Apps page, with a profile for each app": {
    "de": "Die App-Seite von DispHub, mit einem Profil für jede App", "es": "La página Aplicaciones de DispHub, con un perfil para cada aplicación",
    "fr": "La page Applications de DispHub, avec un profil pour chaque application", "it": "La pagina App di DispHub, con un profilo per ogni app",
    "nl": "De pagina Apps van DispHub, met een profiel per app", "pt-br": "A página Apps do DispHub, com um perfil para cada app"},
"DispHub's Schedule page, with today's timeline": {
    "de": "Die Zeitplanseite von DispHub, mit dem heutigen Tagesverlauf", "es": "La página Horario de DispHub, con la línea de tiempo de hoy",
    "fr": "La page Horaire de DispHub, avec la chronologie du jour", "it": "La pagina Programmazione di DispHub, con la sequenza di oggi",
    "nl": "De pagina Schema van DispHub, met de tijdlijn van vandaag", "pt-br": "A página Agendamento do DispHub, com a linha do tempo de hoje"},
">Get DispHub<": {"de": ">DispHub holen<", "es": ">Consigue DispHub<", "fr": ">Obtenir DispHub<", "it": ">Scarica DispHub<", "nl": ">DispHub downloaden<", "pt-br": ">Baixe o DispHub<"},
"Free on the Microsoft Store, with a week of Pro to try. Pro is a one-time upgrade.": {
    "de": "Kostenlos im Microsoft Store, mit einer Woche Pro zum Ausprobieren. Pro ist ein einmaliges Upgrade.",
    "es": "Gratis en Microsoft Store, con una semana de Pro para probar. Pro es una mejora de pago único.",
    "fr": "Gratuit sur le Microsoft Store, avec une semaine de Pro à l'essai. Pro est une mise à niveau en un seul achat.",
    "it": "Gratis su Microsoft Store, con una settimana di Pro da provare. Pro è un upgrade con acquisto unico.",
    "nl": "Gratis in de Microsoft Store, met een week Pro om te proberen. Pro is een eenmalige upgrade.",
    "pt-br": "Grátis na Microsoft Store, com uma semana de Pro para testar. Pro é um upgrade de compra única."},
"Do viewers on my stream see the colours?": {
    "de": "Sehen Zuschauer meines Streams die Farben?", "es": "¿Ven los colores quienes miran mi stream?", "fr": "Les spectateurs de mon stream voient-ils les couleurs ?",
    "it": "Chi guarda il mio stream vede i colori?", "nl": "Zien kijkers van mijn stream de kleuren?", "pt-br": "Quem assiste minha live vê as cores?"},
"Not with OBS, Discord or screenshots: they capture your normal colours. If you use a capture card, it does show DispHub.": {
    "de": "Nicht mit OBS, Discord oder Screenshots: Sie erfassen deine normalen Farben. Mit einer Capture-Karte ist DispHub zu sehen.",
    "es": "No con OBS, Discord ni capturas de pantalla: capturan tus colores normales. Si usas una capturadora, sí se ve DispHub.",
    "fr": "Pas avec OBS, Discord ou les captures d'écran : ils capturent vos couleurs normales. Avec une carte d'acquisition, DispHub est visible.",
    "it": "Non con OBS, Discord o gli screenshot: catturano i tuoi colori normali. Con una scheda di acquisizione DispHub si vede.",
    "nl": "Niet met OBS, Discord of screenshots: die leggen je normale kleuren vast. Met een capturekaart is DispHub wel te zien.",
    "pt-br": "Não com OBS, Discord ou capturas de tela: eles capturam suas cores normais. Com uma placa de captura, o DispHub aparece."},
"Does it slow down my PC?": {"de": "Macht es meinen PC langsamer?", "es": "¿Hace más lento mi PC?", "fr": "Est-ce que ça ralentit mon PC ?", "it": "Rallenta il mio PC?", "nl": "Maakt het mijn pc trager?", "pt-br": "Deixa meu PC mais lento?"},
"No. DispHub uses very little memory, and colours change in your graphics card's output.": {
    "de": "Nein. DispHub braucht sehr wenig Arbeitsspeicher, und die Farben ändern sich im Ausgang deiner Grafikkarte.",
    "es": "No. DispHub usa muy poca memoria y los colores cambian en la salida de tu tarjeta gráfica.",
    "fr": "Non. DispHub utilise très peu de mémoire, et les couleurs changent à la sortie de votre carte graphique.",
    "it": "No. DispHub usa pochissima memoria e i colori cambiano nell'uscita della scheda video.",
    "nl": "Nee. DispHub gebruikt heel weinig geheugen en de kleuren veranderen in de uitvoer van je videokaart.",
    "pt-br": "Não. O DispHub usa pouquíssima memória, e as cores mudam na saída da sua placa de vídeo."},
"Does it work with HDR?": {"de": "Funktioniert es mit HDR?", "es": "¿Funciona con HDR?", "fr": "Est-ce que ça fonctionne avec le HDR ?", "it": "Funziona con l'HDR?", "nl": "Werkt het met HDR?", "pt-br": "Funciona com HDR?"},
"Vibrance does. Windows blocks gamma changes while HDR is on, and profiles can turn HDR on or off for you.": {
    "de": "Die Farbintensität schon. Windows blockiert Gamma-Änderungen, solange HDR an ist, und Profile können HDR für dich ein- oder ausschalten.",
    "es": "La intensidad sí. Windows bloquea los cambios de gamma con HDR activado, y los perfiles pueden activar o desactivar el HDR por ti.",
    "fr": "La vibrance, oui. Windows bloque les changements de gamma quand le HDR est activé, et les profils peuvent activer ou désactiver le HDR pour vous.",
    "it": "La vivacità sì. Windows blocca le modifiche del gamma con l'HDR attivo, e i profili possono attivare o disattivare l'HDR per te.",
    "nl": "Kleurintensiteit wel. Windows blokkeert gammawijzigingen zolang HDR aan staat, en profielen kunnen HDR voor je aan- of uitzetten.",
    "pt-br": "A vibrance funciona. O Windows bloqueia mudanças de gama com o HDR ligado, e os perfis podem ligar ou desligar o HDR para você."},
"Can it change my monitor itself?": {"de": "Kann es meinen Monitor selbst einstellen?", "es": "¿Puede cambiar el propio monitor?", "fr": "Peut-il régler le moniteur lui-même ?", "it": "Può regolare il monitor stesso?", "nl": "Kan het mijn monitor zelf instellen?", "pt-br": "Ele pode ajustar o próprio monitor?"},
"Yes. Profiles can set the screen's own brightness, on laptops and most monitors, its refresh rate and HDR. Laptops can also use a lower refresh rate on battery.": {
    "de": "Ja. Profile können die Helligkeit des Bildschirms selbst einstellen, bei Laptops und den meisten Monitoren, dazu die Bildwiederholrate und HDR. Laptops können im Akkubetrieb auch eine niedrigere Bildwiederholrate nutzen.",
    "es": "Sí. Los perfiles pueden ajustar el brillo de la propia pantalla, en portátiles y en la mayoría de monitores, su frecuencia de actualización y el HDR. Los portátiles también pueden usar una frecuencia menor con batería.",
    "fr": "Oui. Les profils peuvent régler la luminosité de l'écran lui-même, sur les portables et la plupart des moniteurs, sa fréquence de rafraîchissement et le HDR. Les portables peuvent aussi utiliser une fréquence plus basse sur batterie.",
    "it": "Sì. I profili possono impostare la luminosità dello schermo stesso, sui portatili e sulla maggior parte dei monitor, la frequenza di aggiornamento e l'HDR. I portatili possono anche usare una frequenza più bassa a batteria.",
    "nl": "Ja. Profielen kunnen de helderheid van het scherm zelf instellen, op laptops en de meeste monitoren, plus de verversingssnelheid en HDR. Laptops kunnen op de accu ook een lagere verversingssnelheid gebruiken.",
    "pt-br": "Sim. Os perfis podem ajustar o brilho da própria tela, em notebooks e na maioria dos monitores, a taxa de atualização e o HDR. Notebooks também podem usar uma taxa menor na bateria."},
"Does it collect data?": {"de": "Sammelt es Daten?", "es": "¿Recopila datos?", "fr": "Collecte-t-il des données ?", "it": "Raccoglie dati?", "nl": "Verzamelt het gegevens?", "pt-br": "Ele coleta dados?"},
'No. No account, no telemetry, and nothing leaves your PC. See the <a href="privacy/">privacy policy</a>.': {
    "de": 'Nein. Kein Konto, keine Telemetrie, und nichts verlässt deinen PC. Siehe die <a href="privacy/">Datenschutzerklärung</a>.',
    "es": 'No. Sin cuenta, sin telemetría y nada sale de tu PC. Consulta la <a href="privacy/">política de privacidad</a>.',
    "fr": 'Non. Pas de compte, pas de télémétrie, et rien ne quitte votre PC. Consultez la <a href="privacy/">politique de confidentialité</a>.',
    "it": 'No. Nessun account, nessuna telemetria e nulla lascia il tuo PC. Leggi l\'<a href="privacy/">informativa sulla privacy</a>.',
    "nl": 'Nee. Geen account, geen telemetrie, en niets verlaat je pc. Zie het <a href="privacy/">privacybeleid</a>.',
    "pt-br": 'Não. Sem conta, sem telemetria, e nada sai do seu PC. Veja a <a href="privacy/">política de privacidade</a>.'},
">Privacy<": {"de": ">Datenschutz<", "es": ">Privacidad<", "fr": ">Confidentialité<", "it": ">Privacy<", "nl": ">Privacy<", "pt-br": ">Privacidade<"},
">Translate<": {"de": ">Übersetzen<", "es": ">Traducir<", "fr": ">Traduire<", "it": ">Traduci<", "nl": ">Vertalen<", "pt-br": ">Traduzir<"},
">Report a problem<": {"de": ">Problem melden<", "es": ">Informar de un problema<", "fr": ">Signaler un problème<", "it": ">Segnala un problema<", "nl": ">Probleem melden<", "pt-br": ">Relatar um problema<"},
"Free software under the": {"de": "Freie Software unter der", "es": "Software libre bajo la", "fr": "Logiciel libre sous", "it": "Software libero con licenza", "nl": "Vrije software onder de", "pt-br": "Software livre sob a"},
"Photos: U.S. National Park Service": {"de": "Fotos: U.S. National Park Service", "es": "Fotos: U.S. National Park Service", "fr": "Photos : U.S. National Park Service", "it": "Foto: U.S. National Park Service", "nl": "Foto's: U.S. National Park Service", "pt-br": "Fotos: U.S. National Park Service"},
}

START, END = "<!-- languages -->", "<!-- /languages -->"


def url(folder):
    return HOME + (folder + "/" if folder else "")


def head_links():
    # English is for every English-speaking country, though the page itself is written in British English
    links = [f'<link rel="alternate" hreflang="{lang if f else "en"}" href="{url(f)}">' for f, (lang, _) in LANGS.items()]
    links.append(f'<link rel="alternate" hreflang="x-default" href="{HOME}">')
    return f"  {START}\n  " + "\n  ".join(links) + f"\n  {END}\n"


def foot_links(current):
    items = [f'<a href="/{f + "/" if f else ""}"{" aria-current=\"page\"" if f == current else ""} lang="{lang}">{name}</a>'
             for f, (lang, name) in LANGS.items()]
    return f'{START}<nav class="langs" aria-label="Languages">{" · ".join(items)}</nav>{END}'


def strip(html):
    html = re.sub(r"[ \t]*" + re.escape(START) + r".*?" + re.escape(END) + r"\n?", "", html, flags=re.S)
    return html


english = strip((SITE / "index.html").read_text(encoding="utf-8"))
english = english.replace("</head>", head_links() + "</head>", 1)
english = re.sub(r'(<p class="legal">.*?</p>)', lambda m: m.group(1) + "\n      " + foot_links(""), english, count=1, flags=re.S)
(SITE / "index.html").write_text(english, encoding="utf-8", newline="")

for folder, (lang, _) in LANGS.items():
    if not folder:
        continue
    page = english.replace(foot_links(""), foot_links(folder))
    for source, translations in sorted(T.items(), key=lambda kv: -len(kv[0])):
        if folder in translations:
            page = page.replace(source, translations[folder])
    page = page.replace('<html lang="en-GB">', f'<html lang="{lang}">', 1)
    page = page.replace(f'<link rel="canonical" href="{HOME}">', f'<link rel="canonical" href="{url(folder)}">', 1)
    page = page.replace(f'<meta property="og:url" content="{HOME}">', f'<meta property="og:url" content="{url(folder)}">', 1)
    page = page.replace(f'"url": "{HOME}"', f'"url": "{url(folder)}"', 1)
    # one folder down: relative links and pictures start from the site root
    page = re.sub(r'(href|src)="(?!https?:|/|#|\.\./|mailto:)([^"]*)"', lambda m: f'{m.group(1)}="../{m.group(2) if m.group(2) != "./" else ""}"', page)
    page = re.sub(r'srcset="([^"]*)"', lambda m: 'srcset="' + re.sub(r'(^|, )assets/', r'\1../assets/', m.group(1)) + '"', page)
    page = page.replace('<a class="brand" href="../"', '<a class="brand" href="./"')
    out = SITE / folder / "index.html"
    out.parent.mkdir(exist_ok=True)
    out.write_text(page, encoding="utf-8", newline="")
    left = [s for s in T if s in page and folder in T[s] and T[s][folder] != s]
    print(out, "untranslated:", left)
