using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using BlackHole.Authoring;
using BlackHole.Core;
using BlackHole.Unity;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace BlackHole.EditorTools
{
    // 데이터 시트 창(메뉴 BlackHole > Data Sheets). 구글 시트의 값을 에셋으로 가져오고, 에셋의 값을 CSV로 낸다.
    // 지금은 블랙홀 성장 설정(Growth·Milestones 탭)만 다룬다.
    //
    // - 시트에서 가져오기(Ctrl+Alt+I): 탭마다 "웹에 게시"한 CSV 주소에서 받아, 칸 모양과 게임 규칙을 본 뒤 에셋에 쓴다.
    //   오류가 하나라도 있으면 에셋을 건드리지 않고 시트 위치와 함께 알린다.
    // - CSV 폴더에서 가져오기: 내려받은 Growth.csv·Milestones.csv로 같은 일을 한다.
    // - CSV로 내보내기: 지금 에셋의 값을 시트에 붙여 넣을 CSV로 낸다.
    // 주소는 이 PC의 프로젝트 사용자 설정(UserSettings, 커밋하지 않음)에 둔다.
    internal sealed class DataSheetWindow : EditorWindow
    {
        private const string GrowthUrlKey = "BlackHole.DataSheets.GrowthUrl";
        private const string MilestonesUrlKey = "BlackHole.DataSheets.MilestonesUrl";
        private const string FolderKey = "BlackHole.DataSheets.Folder";

        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        private static readonly Color _errorText = new Color(1f, 0.55f, 0.55f);
        private static readonly Color _okText = new Color(0.5f, 0.9f, 0.5f);

        [SerializeField] private HqGrowthSetup _setup;

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

            if (_setup == null)
                _setup = HqGrowthSheetSync.FindSetup();

            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 6;

            var setupField = new ObjectField("블랙홀 성장 설정") { objectType = typeof(HqGrowthSetup), allowSceneObjects = false, value = _setup };
            setupField.RegisterValueChangedCallback(evt => _setup = evt.newValue as HqGrowthSetup);
            root.Add(setupField);

            root.Add(UrlField("Growth 탭 CSV 주소", GrowthUrlKey));
            root.Add(UrlField("Milestones 탭 CSV 주소", MilestonesUrlKey));
            root.Add(Note("주소 얻기: 시트의 파일 → 공유 → 웹에 게시 → 탭을 고르고 형식을 CSV로 → 게시. 탭마다 한 번씩 한다."));

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

            if (_busy || !HasSetup())
                return;

            string growthUrl = EditorUserSettings.GetConfigValue(GrowthUrlKey);
            string milestonesUrl = EditorUserSettings.GetConfigValue(MilestonesUrlKey);

            if (string.IsNullOrWhiteSpace(growthUrl) || string.IsNullOrWhiteSpace(milestonesUrl))
            {
                ShowMessage("Growth·Milestones 탭의 CSV 주소를 먼저 넣는다.", _errorText);
                return;
            }

            _busy = true;
            ShowMessage("시트에서 받는 중…", Color.white);

            try
            {
                string[] tables = await Task.WhenAll(Download(growthUrl), Download(milestonesUrl));
                Apply(tables[0], tables[1], "시트");
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

            if (!HasSetup())
                return;

            string folder = EditorUtility.OpenFolderPanel("Growth.csv·Milestones.csv가 있는 폴더", LastFolder(), string.Empty);

            if (string.IsNullOrEmpty(folder))
                return;

            EditorUserSettings.SetConfigValue(FolderKey, folder);
            string stagesPath = Path.Combine(folder, HqGrowthSheetSync.StagesFile);
            string milestonesPath = Path.Combine(folder, HqGrowthSheetSync.MilestonesFile);

            if (!File.Exists(stagesPath) || !File.Exists(milestonesPath))
            {
                ShowMessage($"폴더에 {HqGrowthSheetSync.StagesFile}와 {HqGrowthSheetSync.MilestonesFile}가 모두 있어야 한다.", _errorText);
                return;
            }

            Apply(File.ReadAllText(stagesPath), File.ReadAllText(milestonesPath), "CSV 파일");
        }

        private void ExportToFolder()
        {
            BuildOnce();

            if (!HasSetup())
                return;

            string folder = EditorUtility.SaveFolderPanel("CSV를 낼 폴더", LastFolder(), string.Empty);

            if (string.IsNullOrEmpty(folder))
                return;

            EditorUserSettings.SetConfigValue(FolderKey, folder);
            HqGrowthSheetSync.Export(_setup, folder);
            ShowMessage($"{HqGrowthSheetSync.StagesFile}, {HqGrowthSheetSync.MilestonesFile}를 냈다. 시트의 같은 이름 탭에 가져오기(파일 → 가져오기 → 현재 시트 바꾸기)로 넣는다.", _okText);
            EditorUtility.RevealInFinder(Path.Combine(folder, HqGrowthSheetSync.StagesFile));
        }

        private void Apply(string stagesCsv, string milestonesCsv, string source)
        {
            bool changed = HqGrowthSheetSync.Apply(_setup, stagesCsv, milestonesCsv, out HqGrowthSheetResult result);

            if (!result.Succeeded)
            {
                _results.Clear();
                _results.Add(Line($"{source}에 오류가 {result.Diagnostics.Count}개 있어 에셋을 바꾸지 않았다.", _errorText));

                foreach (ContentDiagnostic diagnostic in result.Diagnostics)
                    _results.Add(Line(diagnostic.ToString(), _errorText));

                return;
            }

            HqGrowthData data = result.Data;
            string summary = $"성장도 {data.Stages.Count}개 · 이정표 {data.Milestones.Count}개";
            ShowMessage(changed ? $"{source}의 값을 에셋에 썼다({summary})." : $"{source}의 값이 에셋과 같다({summary}). 바꾸지 않았다.", _okText);
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

        private bool HasSetup()
        {
            if (_setup != null)
                return true;

            ShowMessage("블랙홀 성장 설정 에셋(HqGrowthSetup)을 고른다.", _errorText);
            return false;
        }

        private static string LastFolder() => EditorUserSettings.GetConfigValue(FolderKey) ?? string.Empty;

        private static TextField UrlField(string label, string key)
        {
            var field = new TextField(label) { value = EditorUserSettings.GetConfigValue(key) ?? string.Empty, isDelayed = true };
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
