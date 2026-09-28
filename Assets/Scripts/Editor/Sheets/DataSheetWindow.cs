using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using BlackHole.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace BlackHole.EditorTools
{
    // 데이터 시트 창(메뉴 BlackHole > Data Sheets). 구글 시트의 값을 콘텐츠 에셋으로 가져오고, 에셋의 값을 CSV로 낸다.
    // 탭과 에셋의 짝, 검사 순서는 DataSheetImport에 있다.
    //
    // - 시트에서 가져오기(Ctrl+Alt+I): 탭마다 "웹에 게시"한 CSV 주소에서 받는다. 주소를 비운 탭은 가져오지 않는다.
    // - CSV 폴더에서 가져오기: 폴더에 있는 <탭>.csv로 같은 일을 한다.
    // - CSV로 내보내기: 지금 에셋의 값을 탭마다 CSV로 낸다.
    // 주소는 이 PC의 프로젝트 사용자 설정(UserSettings, 커밋하지 않음)에 둔다.
    internal sealed class DataSheetWindow : EditorWindow
    {
        private const string FolderKey = "BlackHole.DataSheets.Folder";

        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        private static readonly Color _errorText = new Color(1f, 0.55f, 0.55f);
        private static readonly Color _okText = new Color(0.5f, 0.9f, 0.5f);
        private static readonly Color _warningText = new Color(1f, 0.85f, 0.45f);

        private VisualElement _results;
        private bool _busy;

        [MenuItem("BlackHole/Data Sheets/Open")]
        public static void Open() => GetWindow<DataSheetWindow>("Data Sheets");

        [MenuItem("BlackHole/Data Sheets/Import from Sheets %&i")]
        private static void ImportFromSheetsMenu()
        {
            DataSheetWindow window = GetWindow<DataSheetWindow>("Data Sheets");
            window.ImportFromSheets();
        }

        // 단축키로 창을 처음 열면 Unity가 CreateGUI를 부르기 전에 가져오기가 시작되므로, 먼저 온 쪽이 한 번만 만든다.
        private void CreateGUI() => BuildOnce();

        private void BuildOnce()
        {
            if (_results != null)
                return;

            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 6;

            foreach (string tab in DataSheetImport.Tabs)
                root.Add(UrlField(tab));

            root.Add(Note("주소 얻기: 시트의 파일 → 공유 → 웹에 게시 → 탭을 고르고 형식을 CSV로 → 게시. 주소를 비운 탭은 가져오지 않는다."));

            root.Add(new Button(ImportFromSheets) { text = "시트에서 가져오기 (Ctrl+Alt+I)" });
            root.Add(new Button(ImportFromFolder) { text = "CSV 폴더에서 가져오기…" });
            root.Add(new Button(ExportToFolder) { text = "CSV로 내보내기…" });

            _results = new VisualElement();
            _results.style.marginTop = 10;
            root.Add(_results);
        }

        private async void ImportFromSheets()
        {
            BuildOnce();

            if (_busy || !TryFindAssets(out ContentAssets assets))
                return;

            var urls = new Dictionary<string, string>();

            foreach (string tab in DataSheetImport.Tabs)
            {
                string url = EditorUserSettings.GetConfigValue(UrlKey(tab));

                if (!string.IsNullOrWhiteSpace(url))
                    urls.Add(tab, url);
            }

            if (urls.Count == 0)
            {
                ShowMessage("탭의 CSV 주소를 먼저 넣는다.", _errorText);
                return;
            }

            _busy = true;
            ShowMessage($"시트에서 탭 {urls.Count}개를 받는 중…", Color.white);

            try
            {
                var tables = new Dictionary<string, string>();
                var downloads = new List<Task<string>>();

                foreach (string url in urls.Values)
                    downloads.Add(Download(url));

                string[] texts = await Task.WhenAll(downloads);
                int i = 0;

                foreach (string tab in urls.Keys)
                    tables.Add(tab, texts[i++]);

                Import(assets, tables, "시트");
            }
            catch (Exception error) when (error is HttpRequestException || error is TaskCanceledException || error is InvalidDataException)
            {
                ShowMessage("받지 못했다: " + error.Message, _errorText);
            }
            finally
            {
                _busy = false;
            }
        }

        private void ImportFromFolder()
        {
            BuildOnce();

            if (!TryFindAssets(out ContentAssets assets))
                return;

            string folder = EditorUtility.OpenFolderPanel("<탭>.csv가 있는 폴더", LastFolder(), string.Empty);

            if (string.IsNullOrEmpty(folder))
                return;

            EditorUserSettings.SetConfigValue(FolderKey, folder);
            var tables = new Dictionary<string, string>();

            foreach (string tab in DataSheetImport.Tabs)
            {
                string path = Path.Combine(folder, DataSheetImport.FileOf(tab));

                if (File.Exists(path))
                    tables.Add(tab, File.ReadAllText(path));
            }

            Import(assets, tables, "CSV 파일");
        }

        private void ExportToFolder()
        {
            BuildOnce();

            if (!TryFindAssets(out ContentAssets assets))
                return;

            string folder = EditorUtility.SaveFolderPanel("CSV를 낼 폴더", LastFolder(), string.Empty);

            if (string.IsNullOrEmpty(folder))
                return;

            EditorUserSettings.SetConfigValue(FolderKey, folder);
            DataSheetImport.Export(assets, folder);
            ShowMessage($"탭 {DataSheetImport.Tabs.Length + 1}개({string.Join(", ", DataSheetImport.Tabs)}, {DataSheetImport.ReferenceTab})를 CSV로 냈다. " +
                "시트의 같은 이름 탭에 파일 → 가져오기 → 현재 시트 바꾸기로 넣는다. " +
                $"{DataSheetImport.ReferenceTab}는 가져오지 않는다(NodeUpgrades의 stat 열 드롭다운 원본).", _okText);
            EditorUtility.RevealInFinder(Path.Combine(folder, DataSheetImport.FileOf(DataSheetImport.Tabs[0])));
        }

        private void Import(ContentAssets assets, Dictionary<string, string> tables, string source)
        {
            var written = new List<string>();
            var unchanged = new List<string>();
            var warnings = new List<ContentDiagnostic>();
            List<ContentDiagnostic> errors = DataSheetImport.Import(assets, tables, written, unchanged, warnings);
            _results.Clear();

            if (errors.Count > 0)
            {
                _results.Add(Line($"{source}에 오류가 {errors.Count}개 있어 에셋을 바꾸지 않았다.", _errorText));

                foreach (ContentDiagnostic error in errors)
                    _results.Add(Line(error.ToString(), _errorText));

                return;
            }

            _results.Add(Line($"{source}에서 탭 {tables.Count}개를 읽었다: {string.Join(", ", tables.Keys)}.", _okText));

            if (written.Count > 0)
                _results.Add(Line("에셋에 썼다: " + string.Join(", ", written), _okText));

            if (unchanged.Count > 0)
                _results.Add(Line("값이 같아 두었다: " + string.Join(", ", unchanged), _okText));

            foreach (ContentDiagnostic warning in warnings)
                _results.Add(Line("확인: " + warning, _warningText));
        }

        private static async Task<string> Download(string url)
        {
            using HttpResponseMessage response = await _http.GetAsync(url.Trim());
            response.EnsureSuccessStatusCode();

            string type = response.Content.Headers.ContentType?.MediaType ?? string.Empty;

            if (type.Contains("html"))
                throw new InvalidDataException("CSV가 아니라 웹 페이지가 왔다. 게시했는지, 주소가 output=csv로 끝나는지 확인한다.");

            return await response.Content.ReadAsStringAsync();
        }

        private bool TryFindAssets(out ContentAssets assets)
        {
            assets = ContentAssets.Find();
            string missing = assets.Missing();

            if (missing == null)
                return true;

            ShowMessage(missing, _errorText);
            return false;
        }

        // 처음 만든 Growth·Milestones 주소와 같은 이름이다(BlackHole.DataSheets.GrowthUrl).
        private static string UrlKey(string tab) => $"BlackHole.DataSheets.{tab}Url";

        private static string LastFolder() => EditorUserSettings.GetConfigValue(FolderKey) ?? string.Empty;

        private static TextField UrlField(string tab)
        {
            string key = UrlKey(tab);
            var field = new TextField($"{tab} 탭 CSV 주소") { value = EditorUserSettings.GetConfigValue(key) ?? string.Empty, isDelayed = true };
            field.RegisterValueChangedCallback(evt => EditorUserSettings.SetConfigValue(key, evt.newValue.Trim()));
            return field;
        }

        private void ShowMessage(string message, Color color)
        {
            _results.Clear();
            _results.Add(Line(message, color));
        }

        private static Label Line(string text, Color color)
        {
            var label = new Label(text);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.color = color;
            label.style.marginBottom = 2;
            return label;
        }

        private static Label Note(string text)
        {
            var label = new Label(text);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.opacity = 0.7f;
            label.style.marginTop = 2;
            label.style.marginBottom = 6;
            return label;
        }
    }
}
