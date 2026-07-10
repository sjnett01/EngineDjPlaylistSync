using System.Globalization;
using System.Threading;

namespace EngineDjPlaylistSync;

internal static class LocalizationManager
{
    public const string SystemDefaultLanguage = "system";

    private static readonly CultureInfo SystemUiCulture = CultureInfo.CurrentUICulture;

    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
        ["Language.System"] = "System default",
        ["Language.English"] = "English",
        ["Language.Spanish"] = "Spanish",
        ["Language.French"] = "French",
        ["Language.German"] = "German",

        ["App.Title"] = "Engine DJ Folder Import Sync",
        ["Button.Browse"] = "Browse...",
        ["Button.Preview"] = "Preview",
        ["Button.MissingFiles"] = "Missing Files",
        ["Button.RefreshPlaylists"] = "Refresh Playlists",
        ["Button.DeleteCheckedMissing"] = "Delete Checked Missing Tracks",
        ["Button.LocateCheckedFiles"] = "Locate Checked Files...",
        ["Button.ImportCheckedTracks"] = "Import checked tracks",
        ["Button.Cancel"] = "Cancel",
        ["CheckBox.DarkMode"] = "Dark mode",
        ["CheckBox.Analysis"] = "Generate BPM/waveform analysis for checked tracks",
        ["CheckBox.KeyDetection"] = "Include key detection",
        ["CheckBox.OfficialAnalyzer"] = "Use official Engine DJ OfflineAnalyzer",
        ["CheckBox.OfficialAnalyzerNotFound"] = "Official Engine DJ OfflineAnalyzer not found on this computer",
        ["CheckBox.CaptureOfficialAnalyzer"] = "Save official analyzer capture + decoded waveform payloads",

        ["Label.PrimaryDb"] = "Primary Engine DB (m.db):",
        ["Label.ImportFolder"] = "Import folder:",
        ["Label.UpdateTarget"] = "Update target:",
        ["Label.AutoDetected"] = "Auto-detected",
        ["Label.Language"] = "Language:",
        ["Label.Scope"] = "Scope:",
        ["Label.SearchFolderDrive"] = "Search folder/drive:",
        ["Label.BpmRange"] = "BPM Range",
        ["Label.ConcurrentTracks"] = "# concurrent tracks to analyse",
        ["Label.KeyNotation"] = "Key Notation",
        ["Label.PlaylistExplanation"] = "How playlists are chosen: the utility first looks for an existing playlist path that matches the selected folder and its parent folders. Example: selecting C:\\Users\\you\\Music\\Folder 2 updates Music/Folder 2 if that playlist already exists. If no matching playlist path exists, the selected folder name is created/used as the root collection. Subfolders are created underneath the detected target. Click Preview to review tracks first, then import only the checked files.",
        ["Label.MissingHelp"] = "Missing files are scanned automatically when this window opens. Tick missing rows to delete them, or select a search folder/drive and click Locate Checked Files to search all subfolders for same-named audio files and update the database path. If files were deliberately moved to another folder, Locate can repair the paths, but moved files may later appear under another playlist when importing from a different subfolder, creating duplicates. To prevent duplicates, delete the missing tracks from the library here, then add them again using the Import window.",
        ["Label.ImportPreviewHeader"] = "Review the tracks that will be imported into the folder collection. Uncheck anything you do not want included, then click Import checked tracks.",

        ["Status.Ready"] = "Ready",
        ["Status.Error"] = "Error",
        ["Status.ScanningPreview"] = "Scanning for preview...",
        ["Status.ImportingSelected"] = "Importing selected tracks...",
        ["Status.ImportComplete"] = "Import complete",
        ["Status.ImportCompleteDetails"] = "Import complete. Files imported: {0}. New tracks: {1}. Playlist entries added: {2}.",
        ["Status.ScanPreparing"] = "Scan progress: preparing file list...",
        ["Status.ScanNoAudio"] = "Scan progress: no audio files found",
        ["Status.ImportNoFiles"] = "Import progress: no files selected",
        ["Status.FindingFiles"] = "{0} - finding files ({1} found): {2}",
        ["Status.ProgressItem"] = "{0} - {1}/{2}: {3}",
        ["Status.Stage"] = "Stage {0} of {1} - {2}",
        ["Status.StageWorking"] = "Working",
        ["Status.Complete"] = "Complete: {0}/{0}",
        ["Status.CheckingMissing"] = "Checking missing files...",
        ["Status.CheckComplete"] = "Check complete",
        ["Status.CheckCompleteDetails"] = "Check complete. Tracks checked: {0}. Missing files: {1}.",
        ["Status.ProgressIdle"] = "Progress: idle",
        ["Status.ProgressNoTracksChecked"] = "Progress: no tracks checked",
        ["Status.ProgressNoTracksSelected"] = "Progress: no tracks selected",
        ["Status.ProgressCount"] = "Progress: {0}/{1}",
        ["Status.CheckingItem"] = "Checking {0}/{1}: {2}",
        ["Status.LocatingMissing"] = "Locating checked missing files...",
        ["Status.LocateCompleteDetails"] = "Locate complete. Updated: {0}. Not found: {1}. Ambiguous: {2}.",
        ["Status.DeletingMissing"] = "Deleting checked missing tracks...",
        ["Status.DeletedMissing"] = "Checked missing tracks deleted",

        ["Dialog.SelectDbTitle"] = "Select Engine DJ m.db",
        ["Dialog.DbFilter"] = "Engine DJ database (*.db)|*.db|All files (*.*)|*.*",
        ["Dialog.SelectImportFolder"] = "Select the parent folder to import, for example C:\\Music\\Test",
        ["Dialog.MissingDatabaseTitle"] = "Missing database",
        ["Dialog.MissingDatabaseMessage"] = "Select a valid Engine DJ m.db file.",
        ["Dialog.MissingImportFolderTitle"] = "Missing import folder",
        ["Dialog.MissingImportFolderMessage"] = "Select a valid import folder.",
        ["Dialog.EngineRunningTitle"] = "Engine DJ is running",
        ["Dialog.EngineRunningMessage"] = "Engine DJ.exe is currently running. Close Engine DJ before changing the database, then click OK to check again. Click Cancel to stop this action.",
        ["Dialog.LoadPlaylistsTitle"] = "Load playlists",
        ["Dialog.MissingScopeTitle"] = "Missing scope",
        ["Dialog.MissingScopeMessage"] = "Select a check scope first.",
        ["Dialog.SelectRepairFolder"] = "Select the drive or folder to search for missing audio files. All subfolders will be searched.",
        ["Dialog.NoTracksSelectedTitle"] = "No tracks selected",
        ["Dialog.SelectTrackImport"] = "Select at least one track to import.",
        ["Dialog.TickTracksLocate"] = "Tick one or more missing tracks to locate first.",
        ["Dialog.TickTracksDelete"] = "Tick one or more missing tracks to delete first.",
        ["Dialog.MissingSearchFolderTitle"] = "Missing search folder",
        ["Dialog.MissingSearchFolderMessage"] = "Select a valid search folder or drive first.",
        ["Dialog.LocateMissingTitle"] = "Locate missing files",
        ["Dialog.LocateMissingConfirm"] = "This will search all subfolders of:\n\n{0}\n\nfor same-named files matching {1} checked missing track(s). When exactly one matching filename is found, the Track.path entry in the Engine DJ database will be updated. A database backup will be created first. Continue?",
        ["Dialog.DeleteMissingTitle"] = "Delete missing tracks from database",
        ["Dialog.DeleteMissingConfirm"] = "This will remove {0} missing track record(s) from the Engine DJ database and remove their playlist entries. No audio files will be deleted. Continue?",

        ["Form.MissingFilesTitle"] = "Missing Files",
        ["Form.ImportPreviewTitle"] = "Preview Import Tracks",

        ["Grid.TrackId"] = "Track ID",
        ["Grid.Title"] = "Title",
        ["Grid.Artist"] = "Artist",
        ["Grid.StoredDbPath"] = "Stored DB path",
        ["Grid.CheckedDiskPath"] = "Checked disk path",
        ["Grid.Status"] = "Status",
        ["Grid.Filename"] = "Filename",
        ["Grid.FolderUnderCollection"] = "Folder under collection",
        ["Grid.StoredEnginePath"] = "Stored Engine path",

        ["Preview.Status.AlreadyInDatabase"] = "Already in DB",
        ["Preview.Status.RelocatedExisting"] = "Relocated existing",
        ["Preview.Status.NewTrack"] = "New track",
        ["Preview.Root"] = "<root>",
        ["Preview.Was"] = "was {0}",
        ["Preview.Section.New"] = "NEW TRACKS - selected by default ({0})",
        ["Preview.Section.Relocated"] = "RELOCATED EXISTING TRACKS - selected to repair path/playlists ({0})",
        ["Preview.Section.Existing"] = "EXISTING TRACKS - not selected by default ({0})",
        ["Preview.Summary"] = "Selected: {0}/{1}. New: {2}/{3}. Relocated: {4}/{5}. Existing: {6}/{7}. New and relocated tracks are selected by default.",

        ["Scope.EntireCollection"] = "Entire Engine Library / Collection: all tracks",

        ["KeyNotation.Sharps"] = "Sharps",
        ["KeyNotation.Flats"] = "Flats",
        ["KeyNotation.OpenKey"] = "Open Key",
        ["KeyNotation.Camelot"] = "Camelot"
    };

    private static readonly Dictionary<string, string> Spanish = new(StringComparer.Ordinal)
    {
        ["Language.System"] = "Predeterminado del sistema",
        ["Language.English"] = "Inglés",
        ["Language.Spanish"] = "Español",
        ["Language.French"] = "Francés",
        ["Language.German"] = "Alemán",
        ["App.Title"] = "Sincronización de importación de carpetas de Engine DJ",
        ["Button.Browse"] = "Examinar...",
        ["Button.Preview"] = "Vista previa",
        ["Button.MissingFiles"] = "Archivos faltantes",
        ["Button.RefreshPlaylists"] = "Actualizar listas",
        ["Button.DeleteCheckedMissing"] = "Eliminar pistas faltantes marcadas",
        ["Button.LocateCheckedFiles"] = "Localizar archivos marcados...",
        ["Button.ImportCheckedTracks"] = "Importar pistas marcadas",
        ["Button.Cancel"] = "Cancelar",
        ["CheckBox.DarkMode"] = "Modo oscuro",
        ["CheckBox.Analysis"] = "Generar análisis de BPM/forma de onda para las pistas marcadas",
        ["CheckBox.KeyDetection"] = "Incluir detección de tonalidad",
        ["CheckBox.OfficialAnalyzer"] = "Usar Engine DJ OfflineAnalyzer oficial",
        ["CheckBox.OfficialAnalyzerNotFound"] = "Engine DJ OfflineAnalyzer oficial no se encontró en este equipo",
        ["CheckBox.CaptureOfficialAnalyzer"] = "Guardar captura del analizador oficial + datos de forma de onda decodificados",
        ["Label.PrimaryDb"] = "Base de datos principal de Engine (m.db):",
        ["Label.ImportFolder"] = "Carpeta de importación:",
        ["Label.UpdateTarget"] = "Destino de actualización:",
        ["Label.AutoDetected"] = "Detectado automáticamente",
        ["Label.Language"] = "Idioma:",
        ["Label.Scope"] = "Ámbito:",
        ["Label.SearchFolderDrive"] = "Carpeta/unidad de búsqueda:",
        ["Label.BpmRange"] = "Rango de BPM",
        ["Label.ConcurrentTracks"] = "N.º de pistas concurrentes para analizar",
        ["Label.KeyNotation"] = "Notación de tonalidad",
        ["Label.PlaylistExplanation"] = "Cómo se eligen las listas: la utilidad primero busca una ruta de lista existente que coincida con la carpeta seleccionada y sus carpetas principales. Ejemplo: al seleccionar C:\\Users\\you\\Music\\Folder 2 se actualiza Music/Folder 2 si esa lista ya existe. Si no existe una ruta de lista coincidente, el nombre de la carpeta seleccionada se crea/usa como colección raíz. Las subcarpetas se crean debajo del destino detectado. Haz clic en Vista previa para revisar las pistas primero y luego importa solo los archivos marcados.",
        ["Label.MissingHelp"] = "Los archivos faltantes se analizan automáticamente cuando se abre esta ventana. Marca las filas faltantes para eliminarlas, o selecciona una carpeta/unidad de búsqueda y haz clic en Localizar archivos marcados para buscar en todas las subcarpetas archivos de audio con el mismo nombre y actualizar la ruta en la base de datos. Si los archivos se movieron deliberadamente a otra carpeta, Localizar puede reparar las rutas, pero esos archivos podrían aparecer luego bajo otra lista al importar desde una subcarpeta distinta, creando duplicados. Para evitar duplicados, elimina aquí las pistas faltantes de la biblioteca y vuelve a añadirlas desde la ventana Importar.",
        ["Label.ImportPreviewHeader"] = "Revisa las pistas que se importarán a la colección de la carpeta. Desmarca lo que no quieras incluir y luego haz clic en Importar pistas marcadas.",
        ["Status.Ready"] = "Listo",
        ["Status.Error"] = "Error",
        ["Status.ScanningPreview"] = "Analizando para vista previa...",
        ["Status.ImportingSelected"] = "Importando pistas seleccionadas...",
        ["Status.ImportComplete"] = "Importación completa",
        ["Status.ImportCompleteDetails"] = "Importación completa. Archivos importados: {0}. Pistas nuevas: {1}. Entradas de lista añadidas: {2}.",
        ["Status.ScanPreparing"] = "Progreso del análisis: preparando lista de archivos...",
        ["Status.ScanNoAudio"] = "Progreso del análisis: no se encontraron archivos de audio",
        ["Status.ImportNoFiles"] = "Progreso de importación: no hay archivos seleccionados",
        ["Status.FindingFiles"] = "{0} - buscando archivos ({1} encontrados): {2}",
        ["Status.ProgressItem"] = "{0} - {1}/{2}: {3}",
        ["Status.Stage"] = "Etapa {0} de {1} - {2}",
        ["Status.StageWorking"] = "Trabajando",
        ["Status.Complete"] = "Completo: {0}/{0}",
        ["Status.CheckingMissing"] = "Comprobando archivos faltantes...",
        ["Status.CheckComplete"] = "Comprobación completa",
        ["Status.CheckCompleteDetails"] = "Comprobación completa. Pistas comprobadas: {0}. Archivos faltantes: {1}.",
        ["Status.ProgressIdle"] = "Progreso: inactivo",
        ["Status.ProgressNoTracksChecked"] = "Progreso: no se comprobaron pistas",
        ["Status.ProgressNoTracksSelected"] = "Progreso: no hay pistas seleccionadas",
        ["Status.ProgressCount"] = "Progreso: {0}/{1}",
        ["Status.CheckingItem"] = "Comprobando {0}/{1}: {2}",
        ["Status.LocatingMissing"] = "Localizando archivos faltantes marcados...",
        ["Status.LocateCompleteDetails"] = "Localización completa. Actualizados: {0}. No encontrados: {1}. Ambiguos: {2}.",
        ["Status.DeletingMissing"] = "Eliminando pistas faltantes marcadas...",
        ["Status.DeletedMissing"] = "Pistas faltantes marcadas eliminadas",
        ["Dialog.SelectDbTitle"] = "Seleccionar m.db de Engine DJ",
        ["Dialog.DbFilter"] = "Base de datos de Engine DJ (*.db)|*.db|Todos los archivos (*.*)|*.*",
        ["Dialog.SelectImportFolder"] = "Selecciona la carpeta principal que se va a importar, por ejemplo C:\\Music\\Test",
        ["Dialog.MissingDatabaseTitle"] = "Base de datos faltante",
        ["Dialog.MissingDatabaseMessage"] = "Selecciona un archivo m.db de Engine DJ válido.",
        ["Dialog.MissingImportFolderTitle"] = "Carpeta de importación faltante",
        ["Dialog.MissingImportFolderMessage"] = "Selecciona una carpeta de importación válida.",
        ["Dialog.EngineRunningTitle"] = "Engine DJ se está ejecutando",
        ["Dialog.EngineRunningMessage"] = "Engine DJ.exe se está ejecutando. Cierra Engine DJ antes de cambiar la base de datos y luego haz clic en Aceptar para volver a comprobar. Haz clic en Cancelar para detener esta acción.",
        ["Dialog.LoadPlaylistsTitle"] = "Cargar listas",
        ["Dialog.MissingScopeTitle"] = "Ámbito faltante",
        ["Dialog.MissingScopeMessage"] = "Selecciona primero un ámbito de comprobación.",
        ["Dialog.SelectRepairFolder"] = "Selecciona la unidad o carpeta donde buscar archivos de audio faltantes. Se buscará en todas las subcarpetas.",
        ["Dialog.NoTracksSelectedTitle"] = "No hay pistas seleccionadas",
        ["Dialog.SelectTrackImport"] = "Selecciona al menos una pista para importar.",
        ["Dialog.TickTracksLocate"] = "Marca una o más pistas faltantes para localizarlas primero.",
        ["Dialog.TickTracksDelete"] = "Marca una o más pistas faltantes para eliminarlas primero.",
        ["Dialog.MissingSearchFolderTitle"] = "Carpeta de búsqueda faltante",
        ["Dialog.MissingSearchFolderMessage"] = "Selecciona primero una carpeta o unidad de búsqueda válida.",
        ["Dialog.LocateMissingTitle"] = "Localizar archivos faltantes",
        ["Dialog.LocateMissingConfirm"] = "Esto buscará en todas las subcarpetas de:\n\n{0}\n\narchivos con el mismo nombre que coincidan con {1} pista(s) faltante(s) marcada(s). Cuando se encuentre exactamente un archivo coincidente, se actualizará la entrada Track.path en la base de datos de Engine DJ. Primero se creará una copia de seguridad de la base de datos. ¿Continuar?",
        ["Dialog.DeleteMissingTitle"] = "Eliminar pistas faltantes de la base de datos",
        ["Dialog.DeleteMissingConfirm"] = "Esto eliminará {0} registro(s) de pista faltante de la base de datos de Engine DJ y sus entradas de lista. No se eliminará ningún archivo de audio. ¿Continuar?",
        ["Form.MissingFilesTitle"] = "Archivos faltantes",
        ["Form.ImportPreviewTitle"] = "Vista previa de pistas a importar",
        ["Grid.TrackId"] = "ID de pista",
        ["Grid.Title"] = "Título",
        ["Grid.Artist"] = "Artista",
        ["Grid.StoredDbPath"] = "Ruta guardada en BD",
        ["Grid.CheckedDiskPath"] = "Ruta comprobada en disco",
        ["Grid.Status"] = "Estado",
        ["Grid.Filename"] = "Nombre de archivo",
        ["Grid.FolderUnderCollection"] = "Carpeta dentro de la colección",
        ["Grid.StoredEnginePath"] = "Ruta guardada de Engine",
        ["Preview.Status.AlreadyInDatabase"] = "Ya está en la BD",
        ["Preview.Status.RelocatedExisting"] = "Existente reubicada",
        ["Preview.Status.NewTrack"] = "Pista nueva",
        ["Preview.Root"] = "<raíz>",
        ["Preview.Was"] = "antes {0}",
        ["Preview.Section.New"] = "PISTAS NUEVAS - seleccionadas por defecto ({0})",
        ["Preview.Section.Relocated"] = "PISTAS EXISTENTES REUBICADAS - seleccionadas para reparar ruta/listas ({0})",
        ["Preview.Section.Existing"] = "PISTAS EXISTENTES - no seleccionadas por defecto ({0})",
        ["Preview.Summary"] = "Seleccionadas: {0}/{1}. Nuevas: {2}/{3}. Reubicadas: {4}/{5}. Existentes: {6}/{7}. Las pistas nuevas y reubicadas se seleccionan por defecto.",
        ["Scope.EntireCollection"] = "Toda la biblioteca/colección de Engine: todas las pistas",
        ["KeyNotation.Sharps"] = "Sostenidos",
        ["KeyNotation.Flats"] = "Bemoles",
        ["KeyNotation.OpenKey"] = "Open Key",
        ["KeyNotation.Camelot"] = "Camelot"
    };

    private static readonly Dictionary<string, string> French = new(StringComparer.Ordinal)
    {
        ["Language.System"] = "Par défaut du système",
        ["Language.English"] = "Anglais",
        ["Language.Spanish"] = "Espagnol",
        ["Language.French"] = "Français",
        ["Language.German"] = "Allemand",
        ["App.Title"] = "Synchronisation d'importation de dossiers Engine DJ",
        ["Button.Browse"] = "Parcourir...",
        ["Button.Preview"] = "Aperçu",
        ["Button.MissingFiles"] = "Fichiers manquants",
        ["Button.RefreshPlaylists"] = "Actualiser les playlists",
        ["Button.DeleteCheckedMissing"] = "Supprimer les pistes manquantes cochées",
        ["Button.LocateCheckedFiles"] = "Localiser les fichiers cochés...",
        ["Button.ImportCheckedTracks"] = "Importer les pistes cochées",
        ["Button.Cancel"] = "Annuler",
        ["CheckBox.DarkMode"] = "Mode sombre",
        ["CheckBox.Analysis"] = "Générer l'analyse BPM/forme d'onde pour les pistes cochées",
        ["CheckBox.KeyDetection"] = "Inclure la détection de tonalité",
        ["CheckBox.OfficialAnalyzer"] = "Utiliser Engine DJ OfflineAnalyzer officiel",
        ["CheckBox.OfficialAnalyzerNotFound"] = "Engine DJ OfflineAnalyzer officiel est introuvable sur cet ordinateur",
        ["CheckBox.CaptureOfficialAnalyzer"] = "Enregistrer la capture de l'analyseur officiel + les données de forme d'onde décodées",
        ["Label.PrimaryDb"] = "Base Engine principale (m.db) :",
        ["Label.ImportFolder"] = "Dossier d'importation :",
        ["Label.UpdateTarget"] = "Cible de mise à jour :",
        ["Label.AutoDetected"] = "Détecté automatiquement",
        ["Label.Language"] = "Langue :",
        ["Label.Scope"] = "Portée :",
        ["Label.SearchFolderDrive"] = "Dossier/lecteur de recherche :",
        ["Label.BpmRange"] = "Plage BPM",
        ["Label.ConcurrentTracks"] = "Nombre de pistes à analyser en parallèle",
        ["Label.KeyNotation"] = "Notation de tonalité",
        ["Label.PlaylistExplanation"] = "Choix des playlists : l'utilitaire recherche d'abord un chemin de playlist existant qui correspond au dossier sélectionné et à ses dossiers parents. Exemple : sélectionner C:\\Users\\you\\Music\\Folder 2 met à jour Music/Folder 2 si cette playlist existe déjà. Si aucun chemin de playlist correspondant n'existe, le nom du dossier sélectionné est créé/utilisé comme collection racine. Les sous-dossiers sont créés sous la cible détectée. Cliquez sur Aperçu pour vérifier les pistes, puis importez uniquement les fichiers cochés.",
        ["Label.MissingHelp"] = "Les fichiers manquants sont analysés automatiquement à l'ouverture de cette fenêtre. Cochez les lignes manquantes pour les supprimer, ou sélectionnez un dossier/lecteur de recherche et cliquez sur Localiser les fichiers cochés pour rechercher dans tous les sous-dossiers les fichiers audio portant le même nom et mettre à jour le chemin dans la base de données. Si les fichiers ont été volontairement déplacés vers un autre dossier, Localiser peut réparer les chemins, mais les fichiers déplacés peuvent ensuite apparaître sous une autre playlist lors d'une importation depuis un autre sous-dossier, créant des doublons. Pour éviter les doublons, supprimez ici les pistes manquantes de la bibliothèque, puis ajoutez-les à nouveau depuis la fenêtre Importer.",
        ["Label.ImportPreviewHeader"] = "Vérifiez les pistes qui seront importées dans la collection du dossier. Décochez ce que vous ne voulez pas inclure, puis cliquez sur Importer les pistes cochées.",
        ["Status.Ready"] = "Prêt",
        ["Status.Error"] = "Erreur",
        ["Status.ScanningPreview"] = "Analyse pour aperçu...",
        ["Status.ImportingSelected"] = "Importation des pistes sélectionnées...",
        ["Status.ImportComplete"] = "Importation terminée",
        ["Status.ImportCompleteDetails"] = "Importation terminée. Fichiers importés : {0}. Nouvelles pistes : {1}. Entrées de playlist ajoutées : {2}.",
        ["Status.ScanPreparing"] = "Progression de l'analyse : préparation de la liste des fichiers...",
        ["Status.ScanNoAudio"] = "Progression de l'analyse : aucun fichier audio trouvé",
        ["Status.ImportNoFiles"] = "Progression de l'importation : aucun fichier sélectionné",
        ["Status.FindingFiles"] = "{0} - recherche des fichiers ({1} trouvés) : {2}",
        ["Status.ProgressItem"] = "{0} - {1}/{2} : {3}",
        ["Status.Stage"] = "Étape {0} sur {1} - {2}",
        ["Status.StageWorking"] = "Traitement",
        ["Status.Complete"] = "Terminé : {0}/{0}",
        ["Status.CheckingMissing"] = "Vérification des fichiers manquants...",
        ["Status.CheckComplete"] = "Vérification terminée",
        ["Status.CheckCompleteDetails"] = "Vérification terminée. Pistes vérifiées : {0}. Fichiers manquants : {1}.",
        ["Status.ProgressIdle"] = "Progression : inactif",
        ["Status.ProgressNoTracksChecked"] = "Progression : aucune piste vérifiée",
        ["Status.ProgressNoTracksSelected"] = "Progression : aucune piste sélectionnée",
        ["Status.ProgressCount"] = "Progression : {0}/{1}",
        ["Status.CheckingItem"] = "Vérification {0}/{1} : {2}",
        ["Status.LocatingMissing"] = "Localisation des fichiers manquants cochés...",
        ["Status.LocateCompleteDetails"] = "Localisation terminée. Mis à jour : {0}. Introuvables : {1}. Ambigus : {2}.",
        ["Status.DeletingMissing"] = "Suppression des pistes manquantes cochées...",
        ["Status.DeletedMissing"] = "Pistes manquantes cochées supprimées",
        ["Dialog.SelectDbTitle"] = "Sélectionner le m.db d'Engine DJ",
        ["Dialog.DbFilter"] = "Base de données Engine DJ (*.db)|*.db|Tous les fichiers (*.*)|*.*",
        ["Dialog.SelectImportFolder"] = "Sélectionnez le dossier parent à importer, par exemple C:\\Music\\Test",
        ["Dialog.MissingDatabaseTitle"] = "Base de données manquante",
        ["Dialog.MissingDatabaseMessage"] = "Sélectionnez un fichier m.db Engine DJ valide.",
        ["Dialog.MissingImportFolderTitle"] = "Dossier d'importation manquant",
        ["Dialog.MissingImportFolderMessage"] = "Sélectionnez un dossier d'importation valide.",
        ["Dialog.EngineRunningTitle"] = "Engine DJ est en cours d'exécution",
        ["Dialog.EngineRunningMessage"] = "Engine DJ.exe est en cours d'exécution. Fermez Engine DJ avant de modifier la base de données, puis cliquez sur OK pour vérifier à nouveau. Cliquez sur Annuler pour arrêter cette action.",
        ["Dialog.LoadPlaylistsTitle"] = "Charger les playlists",
        ["Dialog.MissingScopeTitle"] = "Portée manquante",
        ["Dialog.MissingScopeMessage"] = "Sélectionnez d'abord une portée de vérification.",
        ["Dialog.SelectRepairFolder"] = "Sélectionnez le lecteur ou le dossier où rechercher les fichiers audio manquants. Tous les sous-dossiers seront parcourus.",
        ["Dialog.NoTracksSelectedTitle"] = "Aucune piste sélectionnée",
        ["Dialog.SelectTrackImport"] = "Sélectionnez au moins une piste à importer.",
        ["Dialog.TickTracksLocate"] = "Cochez d'abord une ou plusieurs pistes manquantes à localiser.",
        ["Dialog.TickTracksDelete"] = "Cochez d'abord une ou plusieurs pistes manquantes à supprimer.",
        ["Dialog.MissingSearchFolderTitle"] = "Dossier de recherche manquant",
        ["Dialog.MissingSearchFolderMessage"] = "Sélectionnez d'abord un dossier ou lecteur de recherche valide.",
        ["Dialog.LocateMissingTitle"] = "Localiser les fichiers manquants",
        ["Dialog.LocateMissingConfirm"] = "Cette action recherchera dans tous les sous-dossiers de :\n\n{0}\n\nles fichiers portant le même nom correspondant à {1} piste(s) manquante(s) cochée(s). Lorsqu'un seul fichier correspondant est trouvé, l'entrée Track.path dans la base de données Engine DJ sera mise à jour. Une sauvegarde de la base de données sera créée au préalable. Continuer ?",
        ["Dialog.DeleteMissingTitle"] = "Supprimer les pistes manquantes de la base de données",
        ["Dialog.DeleteMissingConfirm"] = "Cette action supprimera {0} enregistrement(s) de piste manquante de la base de données Engine DJ ainsi que leurs entrées de playlist. Aucun fichier audio ne sera supprimé. Continuer ?",
        ["Form.MissingFilesTitle"] = "Fichiers manquants",
        ["Form.ImportPreviewTitle"] = "Aperçu des pistes à importer",
        ["Grid.TrackId"] = "ID piste",
        ["Grid.Title"] = "Titre",
        ["Grid.Artist"] = "Artiste",
        ["Grid.StoredDbPath"] = "Chemin BD enregistré",
        ["Grid.CheckedDiskPath"] = "Chemin disque vérifié",
        ["Grid.Status"] = "État",
        ["Grid.Filename"] = "Nom du fichier",
        ["Grid.FolderUnderCollection"] = "Dossier dans la collection",
        ["Grid.StoredEnginePath"] = "Chemin Engine enregistré",
        ["Preview.Status.AlreadyInDatabase"] = "Déjà en BD",
        ["Preview.Status.RelocatedExisting"] = "Existant déplacé",
        ["Preview.Status.NewTrack"] = "Nouvelle piste",
        ["Preview.Root"] = "<racine>",
        ["Preview.Was"] = "était {0}",
        ["Preview.Section.New"] = "NOUVELLES PISTES - sélectionnées par défaut ({0})",
        ["Preview.Section.Relocated"] = "PISTES EXISTANTES DÉPLACÉES - sélectionnées pour réparer chemin/playlists ({0})",
        ["Preview.Section.Existing"] = "PISTES EXISTANTES - non sélectionnées par défaut ({0})",
        ["Preview.Summary"] = "Sélectionnées : {0}/{1}. Nouvelles : {2}/{3}. Déplacées : {4}/{5}. Existantes : {6}/{7}. Les pistes nouvelles et déplacées sont sélectionnées par défaut.",
        ["Scope.EntireCollection"] = "Bibliothèque / collection Engine complète : toutes les pistes",
        ["KeyNotation.Sharps"] = "Dièses",
        ["KeyNotation.Flats"] = "Bémols",
        ["KeyNotation.OpenKey"] = "Open Key",
        ["KeyNotation.Camelot"] = "Camelot"
    };

    private static readonly Dictionary<string, string> German = new(StringComparer.Ordinal)
    {
        ["Language.System"] = "Systemstandard",
        ["Language.English"] = "Englisch",
        ["Language.Spanish"] = "Spanisch",
        ["Language.French"] = "Französisch",
        ["Language.German"] = "Deutsch",
        ["App.Title"] = "Engine DJ Ordnerimport-Synchronisierung",
        ["Button.Browse"] = "Durchsuchen...",
        ["Button.Preview"] = "Vorschau",
        ["Button.MissingFiles"] = "Fehlende Dateien",
        ["Button.RefreshPlaylists"] = "Playlists aktualisieren",
        ["Button.DeleteCheckedMissing"] = "Markierte fehlende Titel löschen",
        ["Button.LocateCheckedFiles"] = "Markierte Dateien suchen...",
        ["Button.ImportCheckedTracks"] = "Markierte Titel importieren",
        ["Button.Cancel"] = "Abbrechen",
        ["CheckBox.DarkMode"] = "Dunkler Modus",
        ["CheckBox.Analysis"] = "BPM-/Wellenformanalyse für markierte Titel erzeugen",
        ["CheckBox.KeyDetection"] = "Tonarterkennung einschließen",
        ["CheckBox.OfficialAnalyzer"] = "Offiziellen Engine DJ OfflineAnalyzer verwenden",
        ["CheckBox.OfficialAnalyzerNotFound"] = "Offizieller Engine DJ OfflineAnalyzer wurde auf diesem Computer nicht gefunden",
        ["CheckBox.CaptureOfficialAnalyzer"] = "Offizielle Analyzer-Aufzeichnung + dekodierte Wellenformdaten speichern",
        ["Label.PrimaryDb"] = "Primäre Engine-Datenbank (m.db):",
        ["Label.ImportFolder"] = "Importordner:",
        ["Label.UpdateTarget"] = "Aktualisierungsziel:",
        ["Label.AutoDetected"] = "Automatisch erkannt",
        ["Label.Language"] = "Sprache:",
        ["Label.Scope"] = "Bereich:",
        ["Label.SearchFolderDrive"] = "Suchordner/-laufwerk:",
        ["Label.BpmRange"] = "BPM-Bereich",
        ["Label.ConcurrentTracks"] = "Anzahl gleichzeitig zu analysierender Titel",
        ["Label.KeyNotation"] = "Tonartnotation",
        ["Label.PlaylistExplanation"] = "So werden Playlists ausgewählt: Das Tool sucht zuerst nach einem vorhandenen Playlist-Pfad, der zum ausgewählten Ordner und seinen übergeordneten Ordnern passt. Beispiel: Wenn C:\\Users\\you\\Music\\Folder 2 ausgewählt wird, wird Music/Folder 2 aktualisiert, falls diese Playlist bereits vorhanden ist. Wenn kein passender Playlist-Pfad existiert, wird der Name des ausgewählten Ordners als Root-Sammlung erstellt/verwendet. Unterordner werden unterhalb des erkannten Ziels erstellt. Klicken Sie auf Vorschau, um die Titel zuerst zu prüfen, und importieren Sie dann nur die markierten Dateien.",
        ["Label.MissingHelp"] = "Fehlende Dateien werden beim Öffnen dieses Fensters automatisch geprüft. Markieren Sie fehlende Zeilen, um sie zu löschen, oder wählen Sie einen Suchordner/ein Laufwerk und klicken Sie auf Markierte Dateien suchen, um alle Unterordner nach gleichnamigen Audiodateien zu durchsuchen und den Datenbankpfad zu aktualisieren. Wenn Dateien absichtlich in einen anderen Ordner verschoben wurden, kann Suchen die Pfade reparieren. Verschobene Dateien können jedoch später beim Import aus einem anderen Unterordner unter einer anderen Playlist erscheinen und Duplikate erzeugen. Um Duplikate zu vermeiden, löschen Sie die fehlenden Titel hier aus der Bibliothek und fügen Sie sie anschließend über das Importfenster erneut hinzu.",
        ["Label.ImportPreviewHeader"] = "Prüfen Sie die Titel, die in die Ordnersammlung importiert werden. Entfernen Sie die Markierung für alles, was nicht enthalten sein soll, und klicken Sie dann auf Markierte Titel importieren.",
        ["Status.Ready"] = "Bereit",
        ["Status.Error"] = "Fehler",
        ["Status.ScanningPreview"] = "Scanne für Vorschau...",
        ["Status.ImportingSelected"] = "Importiere ausgewählte Titel...",
        ["Status.ImportComplete"] = "Import abgeschlossen",
        ["Status.ImportCompleteDetails"] = "Import abgeschlossen. Dateien importiert: {0}. Neue Titel: {1}. Playlist-Einträge hinzugefügt: {2}.",
        ["Status.ScanPreparing"] = "Scanfortschritt: Dateiliste wird vorbereitet...",
        ["Status.ScanNoAudio"] = "Scanfortschritt: keine Audiodateien gefunden",
        ["Status.ImportNoFiles"] = "Importfortschritt: keine Dateien ausgewählt",
        ["Status.FindingFiles"] = "{0} - suche Dateien ({1} gefunden): {2}",
        ["Status.ProgressItem"] = "{0} - {1}/{2}: {3}",
        ["Status.Stage"] = "Stufe {0} von {1} - {2}",
        ["Status.StageWorking"] = "In Arbeit",
        ["Status.Complete"] = "Fertig: {0}/{0}",
        ["Status.CheckingMissing"] = "Prüfe fehlende Dateien...",
        ["Status.CheckComplete"] = "Prüfung abgeschlossen",
        ["Status.CheckCompleteDetails"] = "Prüfung abgeschlossen. Titel geprüft: {0}. Fehlende Dateien: {1}.",
        ["Status.ProgressIdle"] = "Fortschritt: inaktiv",
        ["Status.ProgressNoTracksChecked"] = "Fortschritt: keine Titel geprüft",
        ["Status.ProgressNoTracksSelected"] = "Fortschritt: keine Titel ausgewählt",
        ["Status.ProgressCount"] = "Fortschritt: {0}/{1}",
        ["Status.CheckingItem"] = "Prüfe {0}/{1}: {2}",
        ["Status.LocatingMissing"] = "Suche markierte fehlende Dateien...",
        ["Status.LocateCompleteDetails"] = "Suche abgeschlossen. Aktualisiert: {0}. Nicht gefunden: {1}. Mehrdeutig: {2}.",
        ["Status.DeletingMissing"] = "Lösche markierte fehlende Titel...",
        ["Status.DeletedMissing"] = "Markierte fehlende Titel gelöscht",
        ["Dialog.SelectDbTitle"] = "Engine DJ m.db auswählen",
        ["Dialog.DbFilter"] = "Engine DJ-Datenbank (*.db)|*.db|Alle Dateien (*.*)|*.*",
        ["Dialog.SelectImportFolder"] = "Wählen Sie den übergeordneten Ordner für den Import aus, z. B. C:\\Music\\Test",
        ["Dialog.MissingDatabaseTitle"] = "Datenbank fehlt",
        ["Dialog.MissingDatabaseMessage"] = "Wählen Sie eine gültige Engine DJ m.db-Datei aus.",
        ["Dialog.MissingImportFolderTitle"] = "Importordner fehlt",
        ["Dialog.MissingImportFolderMessage"] = "Wählen Sie einen gültigen Importordner aus.",
        ["Dialog.EngineRunningTitle"] = "Engine DJ läuft",
        ["Dialog.EngineRunningMessage"] = "Engine DJ.exe wird gerade ausgeführt. Schließen Sie Engine DJ, bevor Sie die Datenbank ändern, und klicken Sie dann auf OK, um erneut zu prüfen. Klicken Sie auf Abbrechen, um diese Aktion zu stoppen.",
        ["Dialog.LoadPlaylistsTitle"] = "Playlists laden",
        ["Dialog.MissingScopeTitle"] = "Bereich fehlt",
        ["Dialog.MissingScopeMessage"] = "Wählen Sie zuerst einen Prüfbereich aus.",
        ["Dialog.SelectRepairFolder"] = "Wählen Sie das Laufwerk oder den Ordner aus, in dem nach fehlenden Audiodateien gesucht werden soll. Alle Unterordner werden durchsucht.",
        ["Dialog.NoTracksSelectedTitle"] = "Keine Titel ausgewählt",
        ["Dialog.SelectTrackImport"] = "Wählen Sie mindestens einen Titel für den Import aus.",
        ["Dialog.TickTracksLocate"] = "Markieren Sie zuerst einen oder mehrere fehlende Titel zum Suchen.",
        ["Dialog.TickTracksDelete"] = "Markieren Sie zuerst einen oder mehrere fehlende Titel zum Löschen.",
        ["Dialog.MissingSearchFolderTitle"] = "Suchordner fehlt",
        ["Dialog.MissingSearchFolderMessage"] = "Wählen Sie zuerst einen gültigen Suchordner oder ein Laufwerk aus.",
        ["Dialog.LocateMissingTitle"] = "Fehlende Dateien suchen",
        ["Dialog.LocateMissingConfirm"] = "Dadurch werden alle Unterordner von:\n\n{0}\n\nnach gleichnamigen Dateien durchsucht, die zu {1} markierten fehlenden Titel(n) passen. Wenn genau ein passender Dateiname gefunden wird, wird der Track.path-Eintrag in der Engine DJ-Datenbank aktualisiert. Zuerst wird eine Datenbanksicherung erstellt. Fortfahren?",
        ["Dialog.DeleteMissingTitle"] = "Fehlende Titel aus der Datenbank löschen",
        ["Dialog.DeleteMissingConfirm"] = "Dadurch werden {0} fehlende Titeldatensätze aus der Engine DJ-Datenbank und deren Playlist-Einträge entfernt. Es werden keine Audiodateien gelöscht. Fortfahren?",
        ["Form.MissingFilesTitle"] = "Fehlende Dateien",
        ["Form.ImportPreviewTitle"] = "Importtitel-Vorschau",
        ["Grid.TrackId"] = "Titel-ID",
        ["Grid.Title"] = "Titel",
        ["Grid.Artist"] = "Künstler",
        ["Grid.StoredDbPath"] = "Gespeicherter DB-Pfad",
        ["Grid.CheckedDiskPath"] = "Geprüfter Datenträgerpfad",
        ["Grid.Status"] = "Status",
        ["Grid.Filename"] = "Dateiname",
        ["Grid.FolderUnderCollection"] = "Ordner in der Sammlung",
        ["Grid.StoredEnginePath"] = "Gespeicherter Engine-Pfad",
        ["Preview.Status.AlreadyInDatabase"] = "Bereits in DB",
        ["Preview.Status.RelocatedExisting"] = "Verschoben vorhanden",
        ["Preview.Status.NewTrack"] = "Neuer Titel",
        ["Preview.Root"] = "<Stamm>",
        ["Preview.Was"] = "war {0}",
        ["Preview.Section.New"] = "NEUE TITEL - standardmäßig ausgewählt ({0})",
        ["Preview.Section.Relocated"] = "VERSCHOBENE VORHANDENE TITEL - ausgewählt, um Pfad/Playlists zu reparieren ({0})",
        ["Preview.Section.Existing"] = "VORHANDENE TITEL - standardmäßig nicht ausgewählt ({0})",
        ["Preview.Summary"] = "Ausgewählt: {0}/{1}. Neu: {2}/{3}. Verschoben: {4}/{5}. Vorhanden: {6}/{7}. Neue und verschobene Titel sind standardmäßig ausgewählt.",
        ["Scope.EntireCollection"] = "Gesamte Engine-Bibliothek/-Sammlung: alle Titel",
        ["KeyNotation.Sharps"] = "Kreuze",
        ["KeyNotation.Flats"] = "Bs",
        ["KeyNotation.OpenKey"] = "Open Key",
        ["KeyNotation.Camelot"] = "Camelot"
    };

    private static readonly Dictionary<string, Dictionary<string, string>> Localized = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = English,
        ["es"] = Spanish,
        ["fr"] = French,
        ["de"] = German
    };

    public static string RequestedLanguageCode { get; private set; } = SystemDefaultLanguage;
    public static string EffectiveLanguageCode { get; private set; } = "en";

    public static void ApplyLanguage(string? requestedLanguageCode)
    {
        RequestedLanguageCode = NormalizeRequestedLanguage(requestedLanguageCode);
        var culture = RequestedLanguageCode == SystemDefaultLanguage
            ? SystemUiCulture
            : CultureInfo.GetCultureInfo(RequestedLanguageCode);

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        EffectiveLanguageCode = ResolveEffectiveLanguageCode(culture);
    }

    public static IReadOnlyList<LanguageChoice> GetLanguageChoices() => new[]
    {
        new LanguageChoice(SystemDefaultLanguage, Text("Language.System")),
        new LanguageChoice("en", Text("Language.English")),
        new LanguageChoice("es", Text("Language.Spanish")),
        new LanguageChoice("fr", Text("Language.French")),
        new LanguageChoice("de", Text("Language.German"))
    };

    public static string Text(string key)
    {
        if (Localized.TryGetValue(EffectiveLanguageCode, out var selected) && selected.TryGetValue(key, out var value))
            return value;

        return English.TryGetValue(key, out var fallback) ? fallback : key;
    }

    public static string Format(string key, params object?[] args) => string.Format(CultureInfo.CurrentUICulture, Text(key), args);

    public static string ImportPreviewStatusText(ImportPreviewStatus status) => status switch
    {
        ImportPreviewStatus.AlreadyInDatabase => Text("Preview.Status.AlreadyInDatabase"),
        ImportPreviewStatus.RelocatedExisting => Text("Preview.Status.RelocatedExisting"),
        _ => Text("Preview.Status.NewTrack")
    };

    public static string LocalizeProgressStageName(string? stageName)
    {
        var normalized = (stageName ?? string.Empty).Trim();
        return normalized switch
        {
            "Working" or "" => Text("Status.StageWorking"),
            _ => normalized
        };
    }

    private static string NormalizeRequestedLanguage(string? requestedLanguageCode)
    {
        if (string.IsNullOrWhiteSpace(requestedLanguageCode) || string.Equals(requestedLanguageCode, SystemDefaultLanguage, StringComparison.OrdinalIgnoreCase))
            return SystemDefaultLanguage;

        var code = requestedLanguageCode.Trim();
        if (code.Length >= 2)
            code = code[..2].ToLowerInvariant();

        return Localized.ContainsKey(code) ? code : SystemDefaultLanguage;
    }

    private static string ResolveEffectiveLanguageCode(CultureInfo culture)
    {
        var code = culture.TwoLetterISOLanguageName.ToLowerInvariant();
        return Localized.ContainsKey(code) ? code : "en";
    }
}

internal sealed record LanguageChoice(string Code, string DisplayName)
{
    public override string ToString() => DisplayName;
}

internal sealed record ComboBoxChoice<T>(T Value, string DisplayName)
{
    public override string ToString() => DisplayName;
}
