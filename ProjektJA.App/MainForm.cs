using System.Drawing;
using System.Windows.Forms;

namespace ProjektJA.App;

/// <summary>
/// Główne okno aplikacji z elementami wspólnymi dla obu bibliotek.
/// </summary>
internal sealed class MainForm : Form
{
    // Definiujemy niestandardowe kolory
    private static readonly Color PageColor = Color.FromArgb(244, 247, 251);
    private static readonly Color InkColor = Color.FromArgb(30, 41, 59);
    private static readonly Color MutedColor = Color.FromArgb(100, 116, 139);
    private static readonly Color AccentColor = Color.FromArgb(37, 99, 235);

    // Ważna kwestia - przyjmujemy konwencję nazewniczą: zmienne ze znakiem _ (np. _runButton) oznaczają pola naszej klasy
    // Zmienne bez tego znaku to zwykłe zmienne lokalne, któe znikną po wyjściu z funkcji
    private readonly RadioButton _highLevelOption = new() { Text = "C++ — język wysokiego poziomu", AutoSize = true };
    private readonly RadioButton _assemblyOption = new() { Text = "ASM x64 — biblioteka asemblerowa", AutoSize = true };
    private readonly TrackBar _threadSlider = new() { Minimum = 1, Maximum = 64, TickFrequency = 4, SmallChange = 1, LargeChange = 8, Height = 44 };
    private readonly NumericUpDown _threadInput = new() { Minimum = 1, Maximum = 64, Width = 96, TextAlign = HorizontalAlignment.Center };
    private readonly ComboBox _maskSizeCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
    private readonly Label _threadSummary = new() { AutoSize = true, ForeColor = MutedColor };
    private readonly Label _selectedBackend = new() { AutoSize = true, ForeColor = AccentColor, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
    private readonly Label _timeValue = new() { Text = "—", AutoSize = true, ForeColor = InkColor, Font = new Font("Segoe UI", 27F, FontStyle.Bold) };
    private readonly Label _statusPill = new() { AutoSize = true, Padding = new Padding(10, 7, 10, 7), BackColor = Color.FromArgb(226, 232, 240), ForeColor = InkColor, Text = "Inicjalizacja" };

    private readonly Button _loadImageButton = new() { Text = "Wczytaj obraz", Width = 280, Height = 42 };
    private readonly Button _runButton = new() { Text = "Uruchom algorytm", Width = 280, Height = 42, Enabled = false };
    private readonly Button _saveButton = new() { Text = "Zapisz wynik", Width = 280, Height = 42, Enabled = false };
    private readonly Button _benchmarkButton = new() { Text = "Benchmark (CSV)", Width = 280, Height = 42, Enabled = false };

    private readonly PictureBox _sourcePreview = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White };
    private readonly PictureBox _resultPreview = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White };
    private readonly Label _sourcePlaceholder = new() { Text = "Wczytaj własny obraz, aby zobaczyć podgląd.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = MutedColor, Padding = new Padding(24) };
    private readonly Label _resultPlaceholder = new() { Text = "Wynik pojawi się po uruchomieniu algorytmu.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = MutedColor, Padding = new Padding(24) };

    // Znak ? oznacza, że zmienna moze przyjmować wartość null
    private NativeBackend? _highLevelBackend;
    private NativeBackend? _assemblyBackend;
    private Bitmap? _inputBitmap;
    private bool _syncingThreadControls;

    public MainForm()
    {
        Text = "Projekt JA — Filtr rozmycia Gaussa";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(1100, 720);
        ClientSize = new Size(1480, 900);
        BackColor = PageColor;
        ForeColor = InkColor;
        Font = new Font("Segoe UI", 10F);
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildLayout();
        InitializeThreadCount();
        LoadBackends();
        UpdateSelectedBackend();

        _loadImageButton.Click += (_, _) => LoadImage();
        _runButton.Click += (_, _) => RunAlgorithm();
        _saveButton.Click += (_, _) => SaveImage();
        _benchmarkButton.Click += (_, _) => RunBenchmark();
        _highLevelOption.CheckedChanged += (_, _) => UpdateSelectedBackend();
        _assemblyOption.CheckedChanged += (_, _) => UpdateSelectedBackend();
        _threadSlider.ValueChanged += (_, _) => SynchronizeThreadCount(_threadSlider.Value);
        _threadInput.ValueChanged += (_, _) => SynchronizeThreadCount((int)_threadInput.Value);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = PageColor,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 2
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var header = BuildHeader();
        root.Controls.Add(header, 0, 0);

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = PageColor,
            Padding = new Padding(0, 8, 0, 0)
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 326));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(body, 0, 1);

        body.Controls.Add(BuildControlCard(), 0, 0);
        body.Controls.Add(BuildPreviewArea(), 1, 0);
    }

    private Control BuildHeader()
    {
        var header = new Panel { Dock = DockStyle.Fill, BackColor = PageColor };
        var title = new Label
        {
            Text = "Projekt JA",
            AutoSize = true,
            Font = new Font("Segoe UI", 22F, FontStyle.Bold),
            ForeColor = InkColor,
            Location = new Point(2, 2)
        };
        var subtitle = new Label
        {
            Text = "Filtr rozmycia Gaussa (RGB)",
            AutoSize = true,
            Font = new Font("Segoe UI", 10F),
            ForeColor = MutedColor,
            Location = new Point(4, 48)
        };
        _statusPill.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        header.Controls.Add(_statusPill);
        header.Resize += (_, _) => _statusPill.Location = new Point(header.ClientSize.Width - _statusPill.Width - 4, 18);
        return header;
    }

    private Control BuildControlCard()
    {
        var card = CreateCard();
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.White,
            Padding = new Padding(0, 2, 0, 8)
        };
        card.Controls.Add(flow);

        flow.Controls.Add(SectionTitle("Biblioteka"));
        var backendChoice = new FlowLayoutPanel
        {
            Width = 280,
            Height = 72,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 8, 0, 0),
            BackColor = Color.White
        };
        _highLevelOption.Checked = true;
        _highLevelOption.Margin = new Padding(2, 4, 2, 2);
        _assemblyOption.Margin = new Padding(2, 5, 2, 2);
        backendChoice.Controls.Add(_highLevelOption);
        backendChoice.Controls.Add(_assemblyOption);
        flow.Controls.Add(backendChoice);
        flow.Controls.Add(_selectedBackend);

        flow.Controls.Add(SectionTitle("Rozmiar maski Gaussa"));
        _maskSizeCombo.Items.AddRange(["3x3 (Szybka — promień 1 px)", "5x5 (Średnia — promień 2 px)", "7x7 (Głęboka — promień 3 px)"]);
        _maskSizeCombo.SelectedIndex = 0;
        _maskSizeCombo.Margin = new Padding(0, 6, 0, 4);
        flow.Controls.Add(_maskSizeCombo);

        flow.Controls.Add(SectionTitle("Wątki robocze"));
        var processorCount = Math.Clamp(Environment.ProcessorCount, 1, 64);
        _threadSlider.Value = processorCount;
        _threadInput.Value = processorCount;
        _threadSlider.Width = 280;
        _threadSlider.Margin = new Padding(0, 4, 0, 0);
        flow.Controls.Add(_threadSlider);

        var threadRow = new TableLayoutPanel
        {
            Width = 280,
            Height = 38,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 2)
        };
        threadRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        threadRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var threadLabel = new Label
        {
            Text = "Wybrana liczba wątków",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = InkColor,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _threadInput.Anchor = AnchorStyles.Right;
        threadRow.Controls.Add(threadLabel, 0, 0);
        threadRow.Controls.Add(_threadInput, 1, 0);
        flow.Controls.Add(threadRow);
        _threadSummary.Text = $"Wykryte procesory logiczne: {Environment.ProcessorCount}";
        flow.Controls.Add(_threadSummary);

        flow.Controls.Add(SectionTitle("Czas funkcji"));
        var timerBox = new Panel
        {
            Width = 280,
            Height = 68,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(12),
            Margin = new Padding(0, 8, 0, 4)
        };
        _timeValue.Location = new Point(12, 8);
        timerBox.Controls.Add(_timeValue);
        flow.Controls.Add(timerBox);

        flow.Controls.Add(SectionTitle("Dane wejściowe"));
        _loadImageButton.FlatStyle = FlatStyle.Flat;
        _loadImageButton.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _loadImageButton.BackColor = Color.White;
        _loadImageButton.ForeColor = InkColor;
        _loadImageButton.Margin = new Padding(0, 8, 0, 0);
        flow.Controls.Add(_loadImageButton);

        _runButton.FlatStyle = FlatStyle.Flat;
        _runButton.FlatAppearance.BorderSize = 0;
        _runButton.BackColor = Color.FromArgb(203, 213, 225);
        _runButton.ForeColor = Color.FromArgb(100, 116, 139);
        _runButton.Margin = new Padding(0, 8, 0, 0);
        flow.Controls.Add(_runButton);

        _saveButton.FlatStyle = FlatStyle.Flat;
        _saveButton.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _saveButton.FlatAppearance.BorderSize = 1;
        _saveButton.BackColor = Color.FromArgb(226, 232, 240);
        _saveButton.ForeColor = MutedColor;
        _saveButton.Margin = new Padding(0, 8, 0, 0);
        flow.Controls.Add(_saveButton);

        _benchmarkButton.FlatStyle = FlatStyle.Flat;
        _benchmarkButton.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _benchmarkButton.FlatAppearance.BorderSize = 1;
        _benchmarkButton.BackColor = Color.FromArgb(226, 232, 240);
        _benchmarkButton.ForeColor = MutedColor;
        _benchmarkButton.Margin = new Padding(0, 8, 0, 0);
        flow.Controls.Add(_benchmarkButton);

        return card;
    }

    private Control BuildPreviewArea()
    {
        var area = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = PageColor,
            Padding = new Padding(12, 0, 0, 0)
        };
        area.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        area.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        area.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        area.Controls.Add(BuildImageCard("OBRAZ WEJŚCIOWY", _sourcePreview, _sourcePlaceholder), 0, 0);
        area.Controls.Add(BuildImageCard("WYNIK", _resultPreview, _resultPlaceholder), 1, 0);

        return area;
    }

    private static Control BuildImageCard(string title, PictureBox picture, Label placeholder)
    {
        var card = CreateCard();
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.White
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var heading = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = MutedColor
        };
        var previewFrame = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White
        };
        picture.Dock = DockStyle.Fill;
        placeholder.Dock = DockStyle.Fill;
        previewFrame.Controls.Add(picture);
        previewFrame.Controls.Add(placeholder);
        placeholder.BringToFront();

        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(previewFrame, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

    private static Panel CreateCard()
    {
        return new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(18),
            Margin = new Padding(0),
            BorderStyle = BorderStyle.FixedSingle
        };
    }

    private static Label SectionTitle(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = InkColor,
            Margin = new Padding(0, 15, 0, 1)
        };
    }

    // Metoda, która wyciąga nam liczbę wątkó logicznych w procesorze na danym sprzęcie
    // Wywołujemy ją tylko raz w konstruktorze okna
    private void InitializeThreadCount()
    {
        // "Odpytuje" system Windows o liczbę wątków logicznych procesora
        // Dla pewności - przycinamy tą wyciągniętą wartość do przedziału 1...64 metodą Clamp(), aby program nie wyrzucił błędu przy uruchomieniu na procesorze ze np. 128 wątkami
        var detected = Math.Clamp(Environment.ProcessorCount, 1, 64);

        // Ustawia pozycję suwaka na wykrytą liczbę wątków
        _threadSlider.Value = detected;

        // Tą samą wartość wpisuje do okienka tekstowego obok suwaka
        _threadInput.Value = detected;
    }

    // Metoda ładująca nam obie biblioteki dll do pamięci RAM i ustawiającej komunikat o powodzeniu (lub niepowodzeniu) tej operacji
    private void LoadBackends()
    {
        try
        {
            // Wywołuje naszą klasę NativeBackend, wczytuje bibliotekę C++ do pamięci RAM i zapisuje gotowy obiekt w polu klasy _highLevelBacken
            _highLevelBackend = NativeBackend.Load("ProjektJA.HighLevel.dll");
        }

        // Jeśli plik .dll nie istnieje albo brakuje w nim funkcji, łapie wyjątek do zmiennej ex
        catch (Exception ex)
        {
            // Wypisuje treść błędu (ex.Message) w etykiecie pod przyciskami wyboru biblioteki.
            // Znak $ przed cudzysłowem pozwala wstawić zmienną bezpośrednio w klamry {...}
            // Gdyby go nie było, kompilator potraktowałby klamry i kod wewnątrz nich jak zwykły tekst i wyświetlił dosłownie "C++: błąd ({ex.Message})" zamiast "C++: błąd wiadomość_błędu"
            _selectedBackend.Text = $"C++: błąd ({ex.Message})";
        }

        // Tutaj analogicznie, ale dla biblioteki asemblerowej
        //
        // Dlaczego rozbicie na 2 bloki try/catch?
        // Aby program zadziałał nawet wtedy, kiedy tylko 1 biblioteka została poprawnie załadowana
        try
        {
            _assemblyBackend = NativeBackend.Load("ProjektJA.Assembly.dll");
        }

        catch (Exception ex)
        {
            _selectedBackend.Text = $"Asembler: błąd ({ex.Message})";
        }

        // Flaga boolowska, która informuje, czy obie biblioteki zostały poprawnie załadowane do pamięci RAM
        // is null/is not null to lepsza wersja ==nullptr/!-nullptr, bo nie może zostać przeciążona
        // Może zdażyć się, że ktoś przeciąży operator porównania == i napisze przeciążenie błędnie, np. przy porównaniu z null próbuje odczytać pole obiektu - co zaskutukuje błędem
        var bothLoaded = _highLevelBackend is not null && _assemblyBackend is not null;

        // Operator trójargumentowy - zaczyna się od warunku (w naszym przypadku bothLoaded, co oznacza defacto warunek bothLoaded == 1)
        // Następnie znak zapytania "?" i jaką wartość przyjmuje wyrażenie, jeśli warunek jest prawdziwy
        // Po dwukropku ":" znajduje się analogicznie - informacja jaką wartość przyjmuje wyrażenie, jesli waurnek jest fałszywy
        //
        // _statusPill to mała kontrolka tekstowa (etykieta Label) w prawym górnym rogu okna,
        // wyglądająca jak kolorowa "pigułka" z napisem informującym o stanie programu.
        //
        // Mechanicznie działa to tak - do naszej zmiennej _statusPill.Text wpisywana jest wartość tego trójargumentowego operatora
        _statusPill.Text = bothLoaded ? "Obie DLL wczytane" : "Brak biblioteki DLL";

        // Dobieramy kolor w zależności od tego, czy obie biblioteki DLL zostały poprawnie załadowane
        //
        // - BackColor (Background Color) ustawia kolor tła tej pigułki (jasnozielony lub jasnoczerwony).
        // - ForeColor (Foreground Color) ustawia kolor czcionki (napisu) wewnątrz pigułki (ciemnozielony lub ciemnoczerwony).
        _statusPill.BackColor = bothLoaded ? Color.FromArgb(220, 252, 231) : Color.FromArgb(254, 226, 226);
        _statusPill.ForeColor = bothLoaded ? Color.FromArgb(22, 101, 52) : Color.FromArgb(153, 27, 27);
    }

    // Metoda obsługująca przełączanie kropki wyboru (RadioButton) między biblioteką C++ a ASM.
    private void UpdateSelectedBackend()
    {
        // Sprawdza, przy jakiej bibliotece zaznaczona jest kropka w menu i wybiera na tej podstawie odpowiedni wskaźnik
        //
        // _assemblyOption.Checked wywołuje metodę Checked dla naszego pola klasy _assemblyOption - zwraca ona wartość 1 (true), jeśli odpowiednia kropka jest zaznaczona
        var backend = _assemblyOption.Checked ? _assemblyBackend : _highLevelBackend;

        // Etykieta na wypadek błędu linijkę niżej
        var label = _assemblyOption.Checked ? "ASM" : "C++";

        // Kiedy właściwie backend is null?
        // Zmienna 'backend' ma wartość null wtedy, gdy w metodzie LoadBackends() nie udało się wczytać
        // wybranego pliku .dll (np. plik ProjektJA.Assembly.dll nie został jeszcze skompilowany,
        // brakuje go w folderze z plikiem .exe albo rzucił wyjątek podczas ładowania).
        _selectedBackend.Text = backend is null ? $"{label}: biblioteka niedostępna" : $"Aktywna biblioteka: {backend.Name}";
    }

    // Metoda synchronizująca ze sobą suwak (_threadSlider) oraz pole liczbowe (_threadInput).
    private void SynchronizeThreadCount(int value)
    {
        // Strażnik chroniący przed nieskończoną pętlą. Kiedy suwak zmienia wartość pola liczbowego, pole liczbowe próbuje z powrotem zmienić suwak.
        // Jeśli flaga _syncingThreadControls jest już ustawiona na true, funkcja natychmiast się kończy.
        if (_syncingThreadControls) return;

        // Rozpoczęcie aktualizacji kontrolek
        _syncingThreadControls = true;

        // Ustawia suwak na nową wartość value, pilnując, by mieściła się między jego minimum (1) a maksimum (64)
        _threadSlider.Value = Math.Clamp(value, _threadSlider.Minimum, _threadSlider.Maximum);

        // Czemu tutaj robi rzutowanie na int?
        // Bo Kontrolka NumericUpDown w WinForms przechowuje liczby w 128-bitowym typie finansowym decimal, dlatego jej Minimum i Maximum trzeba rzutować na int
        _threadInput.Value = Math.Clamp(value, (int)_threadInput.Minimum, (int)_threadInput.Maximum);

        // Zakończenie aktualizacji kontrolek
        _syncingThreadControls = false;
    }

    // Metoda zwraca odpowiedni rozmiar maski, w zależności od wyboru pozycji na liście rozwijanej (indeksowanie od 0)
    //
    // Co to za dzika składnia?
    // Strzałka => oznacza, że ciało metody składa się z jednego wyrażenia, którego wynik jest od razu zwracany (odpowiednik return ...;)
    private int GetSelectedMaskSize() => _maskSizeCombo.SelectedIndex switch
    {
        // _ oznacza default - w naszym przypadku 0
        1 => 5,
        2 => 7,
        _ => 3
    };

    // Metoda otwierająca okno wyboru pliku i wczytująca wybrany obraz do pamięci RAM.
    private void LoadImage()
    {
        // Tworzy systemowe okienko wyboru pliku. Słówko using gwarantuje automatyczne zwolnienie zasobów okienka przy wyjściu z funkcji.
        // W klamrach ustawiamy tytuł okna, filtr wyświetlanych rozszerzeń, wymóg istnienia pliku (CheckFileExists) i blokadę zaznaczania wielu plików naraz (Multiselect = false)
        //
        // W sekcji Filter, znak pionowej kreski "|" odziedziela to, co widzi użytkownik (opis słowny), od tego, czego szuka Windows
        // Mamy 2 pary - "Pliki obrazów" -> filtr dla Windowsa "*.bmp;*.png;..." oraz "Wszystkie pliki" -> filtr dla Windowsa "*.*"
        using var dialog = new OpenFileDialog
        {
            Title = "Wybierz obraz do podglądu",
            Filter = "Pliki obrazów|*.bmp;*.png;*.jpg;*.jpeg;*.tif;*.tiff|Wszystkie pliki|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        // Wyświetla okienko użytkownikowi - jeśli ten kliknął "Anuluj" lub zamknął okno krzyżykiem (wynik inny niż OK), przerywamy funkcję
        //
        // Co robi dialog.ShowDialog(this)?
        // Zatrzymuje wykonywanie funkcji LoadImage i wyświetla na ekranie okienko wyboru pliku z dysku.
        // Przekazanie this (wskaźnika na nasze główne okno) sprawia, że okienko wyboru pliku blokuje główne okno pod spodem, dopóki użytkownik nie wybierze pliku.
        // Gdy użytkownik kliknie „Otwórz”, metoda zwraca DialogResult.OK. Gdy kliknie „Anuluj” lub X – zwraca DialogResult.Cancel
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        // Wyciąga samo rozszerzenie wybranego obrazka i zmienia je na małe litery
        string extension = Path.GetExtension(dialog.FileName).ToLower();

        // Jeśli wybrany został obrazek nieobsługiwanego typu - wyświetla okienko ostrzeżenia i przerywa funkcję
        if (extension != ".jpg" && extension != ".jpeg" && extension != ".png" && extension != ".bmp")
        {
            // Jakie argumenty przyjmuje MessageBox.Show()?
            // this – okno rodzica (na środku którego wyskoczy komunikat),
            // "Ten format..." – główna treść komunikatu wewnątrz okienka,
            // "Brak wsparcia" – tytuł na górnym pasku małego okienka,
            // MessageBoxButtons.OK – jakie przyciski wyświetlić na dole (tylko przycisk OK),
            // MessageBoxIcon.Warning (lub .Error / .Information) – jaką ikonkę systemową narysować obok tekstu (żółty trójkąt ostrzeżenia, czerwony krzyżyk błędu lub niebieskie „i”).
            MessageBox.Show(this, "Ten format pliku nie jest wspierany. Użyj standardowego JPG, PNG lub BMP.", "Brak wsparcia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // W bloku try/catch wczytujemy piksele z dysku do pamięci RAM (chroniąc się przed uszkodzonymi plikami).
        try
        {
            // Wczytuje obraz z dysku - ponieważ Image.FromFile blokuje plik na dysku, oznaczamy go jako using var (zaraz zostanie zamknięty)
            //
            // Co ląduje w zmiennej loaded?
            // Otwarty uchwyt do pliku na dysku. Gdyby spróbować np. zmienić jego nazwę na pulpicie, Windows wyświetliłby błąd: „Plik jest używany przez inny program”
            using var loaded = Image.FromFile(dialog.FileName);

            // Tworzy w pamięci RAM niezależną kopię pikseli z loaded. Dzięki temu, oryginalny plik zostaje zwolniony
            //
            // Co znaczy zwolniony?
            // Oznacza to, że po wyjściu z bloku try, dzięki słówku using, zmienna loaded zostaje zniszczona i zwalnia blokadę pliku na dysku SSD
            var detachedBitmap = new Bitmap(loaded);

            // Zapamiętujemy wskaźnik na poprzedni obrazek (jeśli użytkownik wczytuje kolejne zdjęcie z rzędu),
            // aby zwolnić jego pamięć RAM dopiero po odpięciu go od kontrolki PictureBox (_sourcePreview).
            //
            // A co, jakby tego nie zrobić?
            // Gdybyśmy nie zapisali starego wskaźnika i nie wywołali na nim .Dispose() na końcu metody,
            // to przy wczytywaniu kolejnych zdjęć (drugiego, trzeciego, dziesiątego...) stare bitmapy
            // zostawałyby w pamięci RAM jako "śmieci" (wyciek pamięci), aż Garbage Collector łaskawie by się uruchomił.
            // Z kolei gdybyśmy wywołali _inputBitmap.Dispose() OD RAZU tutaj (przed podmianą obrazu w _sourcePreview.Image),
            // kontrolka okna próbowałaby jeszcze przez ułamek sekundy narysować na ekranie skasowaną bitmapę i wyrzuciła błąd
            var previousBitmap = _inputBitmap;

            // Zapisuje nową bitmapę (bmp) w polu klasy
            // "Nową" w sensie: świeżo wczytaną przez użytkownika w tym wywołaniu funkcji (w odróżnieniu od starej
            // bitmapy z poprzedniego wczytywania, którą schowaliśmy przed chwilą do zmiennej previousBitmap).
            // Fizycznie jest to nasza kopia pikseli w pamięci RAM (detachedBitmap), całkowicie odłączona od pliku na dysku.
            _inputBitmap = detachedBitmap;

            // Wyświetla tą bitmapę w lewym okienku podglądu
            _sourcePreview.Image = detachedBitmap;

            // Ukrywa szary napis "Wczytaj własny obraz...", żeby odsłonić wczytany obrazek
            _sourcePlaceholder.Visible = false;

            // Resetuje licznik czasu wykonania programu z poprzedniego uruchomienia
            _timeValue.Text = "—";

            // Odblokowuje przycisk "Uruchom algorytm"
            _runButton.Enabled = true;

            // Po odblokowaniu algorytmu zmienia jego kolor
            _runButton.BackColor = AccentColor;
            _runButton.ForeColor = Color.White;

            // Jeśli OBIE biblioteki DLL są wczytane, odblokowuje również przycisk wykonujący benchmark i odpowiednio go koloruje
            if (_highLevelBackend is not null && _assemblyBackend is not null)
            {
                _benchmarkButton.Enabled = true;

                _benchmarkButton.BackColor = Color.White;
                _benchmarkButton.ForeColor = InkColor;
            }

            // Zapisujemy wskaźnik na stary wynik programu do tymczasowej zmiennej
            var previousResult = _resultPreview.Image;

            // Odpinamy obrazek od kontrolki , aby okno przestało go rysować
            _resultPreview.Image = null;

            // Wstawiamy na jego miejsce placeholder
            _resultPlaceholder.Visible = true;

            // Dopiero teraz kasujemy stary wynik z pamięci RAM
            // Gdybyśmy od razu zrobili Dispose() bez odpięcia obrazka od kontrolki, ta próbowałaby wtedy narysować niesitniejący obrazek w oknie, co skutkowałoby błędem
            previousResult?.Dispose();

            // Wyłącza i wyszarza przycisk "Zapisz wynik" (bo dla nowo wczytanego zdjęcia nie uruchomiliśmy jeszcze filtra)
            _saveButton.Enabled = false;

            _saveButton.BackColor = Color.FromArgb(226, 232, 240);
            _saveButton.ForeColor = MutedColor;

            // Stary obraz wejściowy nie jest już wyświetlany w _sourcePreview, więc bezpiecznie zwalniamy go z RAM-u.
            previousBitmap?.Dispose();
        }

        // Jeśli plik obrazu był uszkodzony, wyświetla czerwone okienko z opisem błędu
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Nie udało się wczytać obrazu.\n\n{ex.Message}", "Błąd wczytywania", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // Metoda otwierająca okno zapisu pliku i zapisująca tenże plik, o wybranym rozszerzeniu, w wybranym przez użytkownika miejscu
    private void SaveImage()
    {
        // Zabezpieczenie (Guard Clause): sprawdzamy, czy w prawym okienku podglądu faktycznie znajduje się
        // przetworzony obrazek. Gdyby z jakiegoś powodu (np. błędu) obrazka tam nie było (is null),
        // natychmiast przerywamy funkcję, żeby nie próbować zapisać pustego wskaźnika na dysk.
        if (_resultPreview.Image is null) return;

        using var dialog = new SaveFileDialog
        {
            Title = "Zapisz przetworzony obraz",
            Filter = "Obraz PNG (*.png)|*.png|Mapa bitowa BMP (*.bmp)|*.bmp|Obraz JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg",
            DefaultExt = "png",
            FileName = "wynik_gauss"
        };

        if (dialog.ShowDialog() != DialogResult.OK) return;

        try
        {
            var ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();

            // Czym jest ext switch?
            // To nowoczesna instrukcja 'switch' w C#, która bada zawartość zmiennej 'ext' (czyli rozszerzenie pliku, np. ".jpg")
            // i na jej podstawie zwraca odpowiedni obiekt formatu zapisu (ImageFormat.Bmp, Jpeg lub Png) wprost do zmiennej 'format'.
            // Słowo 'or' pozwala połączyć dwa przypadki (".jpg" lub ".jpeg") w jednej linijce, a znak '_' działa jak 'default'.
            var format = ext switch
            {
                ".bmp" => System.Drawing.Imaging.ImageFormat.Bmp,
                ".jpg" or ".jpeg" => System.Drawing.Imaging.ImageFormat.Jpeg,
                _ => System.Drawing.Imaging.ImageFormat.Png
            };

            // Zapisuje gotowy obraz z prawego okna na dysk

            // Jak działa składnia?
            // _resultPreview.Image -> sięgamy do prawego okienka podglądu i bierzemy przechowywany w nim obiekt przetworzonego obrazu,
            // .Save(...) -> wywołujemy na tym obrazie wbudowaną metodę zapisu na dysk,
            // dialog.FileName -> pierwszy argument to pełna ścieżka docelowa wybrana przez użytkownika w oknie (np. "C:\Pulpit\wynik_gauss.png"),
            // format -> drugi argument to wybrany wyżej enkoder graficzny (BMP, JPEG lub PNG), który określa, jak skompresować piksele do pliku.
            _resultPreview.Image.Save(dialog.FileName, format);
        }

        catch (Exception ex)
        {
            MessageBox.Show(this, $"Nie udało się zapisać obrazu.\n\n{ex.Message}", "Błąd zapisu", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // Główna metoda wykonawcza uruchamiana przyciskiem "Uruchom algorytm"
    // Konwertuje obraz do formatu 24-bitowego BGR, blokuje go w RAM-ie, dzieli na paski dla wątków,
    // wywołuje wybraną bibliotekę (C++ lub ASM), mierzy czas wykonania i wyświetla wynik.
    //
    // Co znaczy async?
    // Oznacza, że funkcja może poczekać na zakończenie wątków w tle (await) bez zamrażania okienka programu.
    private async void RunAlgorithm()
    {
        // Jeśli nie wczytano jeszcze żadnego obrazu, przerywamy działanie
        if (_inputBitmap is null) return;

        // Wybieramy wskaźnik na bibliotekę (C++ lub ASM) w zależności od zaznaczonej kropki (RadioButton)
        NativeBackend? selectedBackend = _highLevelOption.Checked ? _highLevelBackend : _assemblyBackend;

        // Jeśli wybrana biblioteka nie została wcześniej poprawnie wczytana do RAM-u, pokazujemy błąd
        if (selectedBackend is null)
        {
            MessageBox.Show(this, "Wybrana biblioteka DLL nie została poprawnie załadowana.", "Błąd backendu", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // Pobieramy rozmiar wybranej maski z listy rozwijanej (3, 5 lub 7)
        int maskSize = GetSelectedMaskSize();

        // Obliczamy promień maski (przy dzieleniu inta przez 2 wynik zaokrąglany jest w dół: 3/2=1, 5/2=2, 7/2=3)
        int radius = maskSize / 2;

        // Zabezpieczenie przed obrazkiem mniejszym lub równym samej masce (np. obrazek 3x3 px dla maski 3x3)
        if (_inputBitmap.Width <= maskSize || _inputBitmap.Height <= maskSize)
        {
            MessageBox.Show(this, $"Obraz jest zbyt mały dla maski {maskSize}x{maskSize}.", "Zbyt mały obraz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Po co cast na int?
        // Kontrolka NumericUpDown (_threadInput) przechowuje wartość .Value w 128-bitowym typie decimal,
        // dlatego musimy jawnie zrzutować ją na 32-bitowy typ całkowity int.
        int threads = (int)_threadInput.Value;

        // Blokujemy wszystkie przyciski na czas wykonywania algorytmu, żeby użytkownik nie kliknął ich ponownie w trakcie pracy
        _runButton.Enabled = false;
        _loadImageButton.Enabled = false;
        _benchmarkButton.Enabled = false;

        // Tworzy w pamięci RAM tymczasową kopię całego obrazu wejściowego (Rectangle od 0,0 do Width,Height),
        // wymuszając konwersję pikseli na format 24-bitowy (Format24bppRgb = dokładnie 3 bajty [B, G, R] na piksel,
        // jakich wymaga nasz kod w C++ i ASM).
        // 'using' automatycznie usunie tę kopię po wyjściu z metody.
        using Bitmap normalizedInput = _inputBitmap.Clone(new Rectangle(0, 0, _inputBitmap.Width, _inputBitmap.Height), System.Drawing.Imaging.PixelFormat.Format24bppRgb);

        // Tworzy w pamięci RAM nową, pustą bitmapę wynikową o tych samych wymiarach i formacie 24-bitowym,
        // do której kod C++/ASM zapisze rozmyty obraz.
        // Nie dajemy tu 'using', bo na końcu wstawimy ją do okienka podglądu
        Bitmap outputBitmap = new Bitmap(normalizedInput.Width, normalizedInput.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

        // Określamy prostokąt obejmujący cały obraz i wywołujemy LockBits na obu bitmapach.
        // LockBits "przybija" tablice pikseli do stałych adresów fizycznych w pamięci RAM (chroniąc je przed
        // przesunięciem przez Garbage Collector w trakcie pracy Asemblera) i daje dostęp do surowych wskaźników Scan0.
        var rect = new Rectangle(0, 0, normalizedInput.Width, normalizedInput.Height);
        var inData = normalizedInput.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, normalizedInput.PixelFormat);
        var outData = outputBitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.WriteOnly, outputBitmap.PixelFormat);

        try
        {
            // Wyciągamy wymiary obrazu oraz szerokość wiersza w bajtach (Stride -> trafi do rejestru R9)
            int width = inData.Width;
            int height = inData.Height;
            int stride = inData.Stride;

            // Scan0 to surowy 64-bitowy adres pierwszego bajtu obrazu w pamięci RAM (trafi do RCX oraz RDX)
            nint inScan0 = inData.Scan0;
            nint outScan0 = outData.Scan0;

            // Obliczamy wysokość roboczą po odjęciu górnej i dolnej ramki o grubości radius,
            // aby maska w C++/ASM nie wyszła poza zaalokowaną pamięć RAM obrazu.
            int usableHeight = height - 2 * radius;

            // Dzielimy bezpieczne wiersze równo pomiędzy wybraną liczbę wątków
            // oraz liczymy resztę z dzielenia (%), aby rozdać pozostałe pojedyncze wiersze.
            int rowsPerThread = usableHeight / threads;
            int remainder = usableHeight % threads;

            // Tablica przechowująca uchwyty do uruchomionych zadań (wątków) w tle
            Task[] tasks = new Task[threads];

            // Pierwszy bezpieczny wiersz do przetworzenia zaczyna się od indeksu 'radius' (omijamy górny brzeg)
            int currentY = radius;

            // Uruchamiamy precyzyjny stoper sprzętowy tuż przed rozdzieleniem pracy na wątki
            var sw = System.Diagnostics.Stopwatch.StartNew();

            for (int t = 0; t < threads; t++)
            {
                // Wyznaczamy początek (startY) i koniec (endY) paska wierszy dla wątku nr 't'.
                // Pierwsze 'remainder' wątków dostaje o 1 wiersz więcej, dzięki czemu 100% obrazu zostaje przetworzone.
                // Uwaga: startY i endY deklarujemy wewnątrz pętli, aby każdy wątek miał własną kopię tych zmiennych!
                int startY = currentY;
                int endY = startY + rowsPerThread + (t < remainder ? 1 : 0);
                currentY = endY;

                // Zlecamy wykonanie paska startY..endY wątkowi roboczemu w tle
                tasks[t] = Task.Run(() =>
                {
                    // Zabezpieczenie na wypadek, gdyby liczba wątków była większa niż liczba wierszy obrazu
                    if (startY < endY)
                    {
                        // Wywołujemy naszą funkcję z DLL (C++ lub ASM) dla przydzielonego paska wierszy
                        int status = selectedBackend.Process(inScan0, outScan0, width, stride, startY, endY, maskSize);
                        if (status != 0)
                        {
                            throw new Exception($"Biblioteka natywna zwróciła błąd (Kod: {status}).");
                        }
                    }
                });
            }

            // Czekamy asynchronicznie (bez zamrażania okna GUI), aż wszystkie wątki w tablicy zakończą pracę
            await Task.WhenAll(tasks);

            // Natychmiast po zakończeniu pracy ostatniego wątku zatrzymujemy stoper
            sw.Stop();

            // Odpinamy obie tablice pikseli w pamięci RAM (zwracamy kontrolę nad pamięcią środowisku .NET)
            normalizedInput.UnlockBits(inData);
            outputBitmap.UnlockBits(outData);

            // Bezpiecznie podmieniamy stary obrazek wynikowy na nowy w prawym okienku i zwalniamy stary z RAM-u
            var oldResult = _resultPreview.Image;
            _resultPreview.Image = outputBitmap;
            _resultPlaceholder.Visible = false;
            oldResult?.Dispose();

            // Odblokowujemy przycisk zapisu wyniku
            _saveButton.Enabled = true;
            _saveButton.BackColor = Color.White;
            _saveButton.ForeColor = InkColor;

            // Wypisujemy zmierzony czas w milisekundach z dokładnością do 2 miejsc po przecinku (:F2)
            _timeValue.Text = $"{sw.Elapsed.TotalMilliseconds:F2} ms";
        }
        catch (Exception ex)
        {
            // W razie błędu w którymś z wątków: odpinamy pamięć LockBits i niszczymy niedokończoną bitmapę wynikową
            normalizedInput.UnlockBits(inData);
            outputBitmap.UnlockBits(outData);
            outputBitmap.Dispose();

            _timeValue.Text = "Błąd";

            MessageBox.Show(this, $"Wystąpił błąd podczas przetwarzania:\n\n{ex.InnerException?.Message ?? ex.Message}", "Błąd wykonania", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        // Instrukcje w bloku finally wykonają się ZAWSZE (zarówno po sukcesie, jak i po błędzie)
        finally
        {
            // Po wykonaniu algorytmu z powrotem włączamy przyciski zablokowane na początku metody
            _runButton.Enabled = true;
            _loadImageButton.Enabled = true;

            // Sprawdzamy ponownie, czy obie DLL są wczytane, zanim z powrotem odblokujemy przycisk benchmarku
            if (_highLevelBackend is not null && _assemblyBackend is not null)
            {
                _benchmarkButton.Enabled = true;
            }
        }
    }

    // Automatyczny test wydajnościowy (Benchmark): uruchamia obie biblioteki dla każdej liczby wątków od 1 do 64
    // (po 10 powtórzeń na każdy przypadek) i zapisuje tabelę porównawczą do pliku CSV (do wykresów w Excelu).
    private async void RunBenchmark()
    {
        // Guard Clause: benchmark porównuje C++ i ASM na obrazie. Jeśli brakuje obrazu lub którejkolwiek biblioteki, wychodzimy.
        if (_inputBitmap is null || _highLevelBackend is null || _assemblyBackend is null) return;

        int maskSize = GetSelectedMaskSize();

        // Dlaczego nie wywołujemy tu po prostu RunAlgorithm()?
        // Ponieważ benchmark wykonuje 64 (wątki) * 2 (biblioteki) * 11 (1 rozgrzewka + 10 pomiarów) = 1408 przebiegów!
        // Gdybyśmy wywołali RunAlgorithm() 1408 razy, program 1408 razy klonowałby bitmapę i odrysowywał ekran.
        // Tutaj przygotowujemy i blokujemy pamięć (LockBits) TYLKO RAZ przed pętlą i mierzymy czysty kod C++/ASM!
        if (_inputBitmap.Width <= maskSize || _inputBitmap.Height <= maskSize)
        {
            MessageBox.Show(this, $"Obraz jest zbyt mały dla maski {maskSize}x{maskSize}.", "Zbyt mały obraz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Okienko wyboru miejsca zapisu wygenerowanego pliku .csv
        using var dialog = new SaveFileDialog
        {
            Title = "Zapisz wyniki benchmarku do CSV",
            Filter = "Plik CSV (*.csv)|*.csv",
            DefaultExt = "csv",
            FileName = $"benchmark_{_inputBitmap.Width}x{_inputBitmap.Height}_maska{maskSize}x{maskSize}.csv"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        // Pobieramy maksymalną liczbę wątków z kontrolki (64) i ustalamy 10 powtórzeń na każdy pomiar
        int maxThreads = (int)_threadInput.Maximum;
        const int iterations = 10;

        // Wyłączamy przyciski na czas działania benchmarku
        _runButton.Enabled = false;
        _loadImageButton.Enabled = false;
        _benchmarkButton.Enabled = false;

        // Jednorazowe przygotowanie buforów 24-bitowych i przypięcie ich w pamięci RAM (LockBits) na czas całego benchmarku.
        // Obie bitmapy mają 'using', bo po zapisaniu pliku CSV mogą zostać automatycznie usunięte z pamięci RAM.
        using Bitmap normalizedInput = _inputBitmap.Clone(
            new Rectangle(0, 0, _inputBitmap.Width, _inputBitmap.Height),
            System.Drawing.Imaging.PixelFormat.Format24bppRgb);

        using Bitmap outputBitmap = new Bitmap(normalizedInput.Width, normalizedInput.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

        var rect = new Rectangle(0, 0, normalizedInput.Width, normalizedInput.Height);
        var inData = normalizedInput.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, normalizedInput.PixelFormat);
        var outData = outputBitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.WriteOnly, outputBitmap.PixelFormat);

        // StringBuilder to szybki bufor tekstowy w pamięci RAM, w którym budujemy kolejne wiersze pliku CSV
        var csvBuilder = new System.Text.StringBuilder();
        csvBuilder.AppendLine("Liczba watkow;Sredni czas C++ [ms];Sredni czas ASM [ms];Przyspieszenie (C++/ASM)");

        // Pobieramy polskie formatowanie regionalne (przecinek jako separator ułamków zamiast kropki),
        // aby polski MS Excel poprawnie rozpoznawał liczby w pliku CSV i nie zamieniał ich na daty.
        var plCulture = System.Globalization.CultureInfo.GetCultureInfo("pl-PL");

        try
        {
            // Pętla testująca po kolei każdą liczbę wątków od 1 do 64
            for (int threads = 1; threads <= maxThreads; threads++)
            {
                // Mierzymy średni czas z 10 przebiegów dla C++ oraz dla Asemblera
                double cppAvg = await MeasureBackendAsync(_highLevelBackend, inData, outData, threads, iterations, maskSize);
                double asmAvg = await MeasureBackendAsync(_assemblyBackend, inData, outData, threads, iterations, maskSize);

                // Obliczamy ilukrotnie Asembler był szybszy od C++ (np. 3,5x)
                double speedup = asmAvg > 0 ? cppAvg / asmAvg : 0;

                // Dopisujemy sformatowany wiersz wyników (oddzielony średnikami) do bufora CSV
                csvBuilder.AppendLine(string.Format(plCulture, "{0};{1:F4};{2:F4};{3:F2}", threads, cppAvg, asmAvg, speedup));
            }

            // Zapisujemy cały wygenerowany tekst z bufora do pliku .csv na dysku w kodowaniu UTF-8
            File.WriteAllText(dialog.FileName, csvBuilder.ToString(), System.Text.Encoding.UTF8);
            MessageBox.Show(this, $"Zakończono pełny test (1–{maxThreads} wątków, maska {maskSize}x{maskSize})!\nWyniki zapisano w:\n{dialog.FileName}", "Benchmark gotowy", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            // Czym jest ex.InnerException?.Message ?? ex.Message?
            // Gdy błąd wystąpi wewnątrz wątku Task.Run, .NET pakuje go w ogólny wyjątek AggregateException,
            // a prawdziwą przyczynę błędu chowa głębiej w polu InnerException. Ten zapis wyciąga właściwą treść błędu.
            MessageBox.Show(this, $"Błąd podczas benchmarku:\n\n{ex.InnerException?.Message ?? ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            // Zawsze na końcu benchmarku odpinamy pamięć RAM obu bitmap i odblokowujemy przyciski
            normalizedInput.UnlockBits(inData);
            outputBitmap.UnlockBits(outData);

            _runButton.Enabled = true;
            _loadImageButton.Enabled = true;
            _benchmarkButton.Enabled = true;
        }
    }

    // Metoda pomocnicza dla benchmarku: mierzy średni czas wykonania (w ms) pojedynczej biblioteki (C++ lub ASM)
    // dla zadanej liczby wątków (threads) na przestrzeni 'iterations' powtórzeń (np. 10 razy).
    private async Task<double> MeasureBackendAsync(
        NativeBackend backend,
        System.Drawing.Imaging.BitmapData inData,
        System.Drawing.Imaging.BitmapData outData,
        int threads,
        int iterations,
        int maskSize)
    {
        // Wyliczamy podział wierszy na paski dokładnie tak samo jak w RunAlgorithm()
        int radius = maskSize / 2;
        int usableHeight = inData.Height - 2 * radius;
        int rowsPerThread = usableHeight / threads;
        int remainder = usableHeight % threads;

        // Lokalna funkcja pomocnicza wykonująca dokładnie jeden pełny przebieg wielowątkowy po całym obrazie
        Task RunSinglePassAsync()
        {
            Task[] tasks = new Task[threads];
            int currentY = radius;

            for (int t = 0; t < threads; t++)
            {
                int startY = currentY;
                int endY = startY + rowsPerThread + (t < remainder ? 1 : 0);
                currentY = endY;

                tasks[t] = Task.Run(() =>
                {
                    if (startY < endY)
                    {
                        int status = backend.Process(inData.Scan0, outData.Scan0, inData.Width, inData.Stride, startY, endY, maskSize);
                        if (status != 0) throw new Exception($"Kod błędu DLL: {status}");
                    }
                });
            }
            return Task.WhenAll(tasks);
        }

        // PRZEBIEG ROZGRZEWKOWY (Warm-up pass) wykonywany BEZ stopera:
        // Rozbudza wątki w puli systemowej .NET i wczytuje bajty obrazu z pamięci RAM do szybkiej pamięci Cache procesora,
        // aby właściwe pomiary stoperem nie były przekłamane narzutem startowym.
        await RunSinglePassAsync();

        double totalMs = 0;

        // Wykonujemy 'iterations' (10) mierzonych przebiegów i sumujemy czasy ich wykonania
        for (int i = 1; i <= iterations; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await RunSinglePassAsync();
            sw.Stop();
            totalMs += sw.Elapsed.TotalMilliseconds;
        }

        // Wyliczamy średnią arytmetyczną z 10 pomiarów, aktualizujemy podgląd czasu w oknie i zwracamy wynik
        double avgMs = totalMs / iterations;
        _timeValue.Text = $"{avgMs:F2} ms";
        return avgMs;
    }

    // Nadpisuje (override) wbudowaną metodę klasy Form, wywoływaną automatycznie przez Windows w momencie zamknięcia okna
    //
    // Czemu protected?
    // Ponieważ twórcy Windowsa w klasie bazowej Form zadeklarowali metodę OnFormClosed jako protected virtual.
    // W C# nadpisując metodę (override), musimy zachować dokładnie taki sam modyfikator dostępu, jaki nadał jej autor klasy bazowej
    //
    // Co jakbyśmy wykorzystali oryginalną metodę i nic w niej nie nadpisywali?
    // Oryginalna metoda z klasy Form zamknęłaby okienko i posprzątała przyciski, ale nie ma pojęcia o istnieniu naszych własnych pól _inputBitmap, _highLevelBackend i _assemblyBackend,
    // które zostałyby w pamięci aż do ubicia procesu przez system
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        // Jeśli dane obiekty istnieją w pamięci (znak ? przed kropką oznacza is not null), zwalnia z pamięci RAM wczytaną bitmapę, bitmapę wynikową oraz odładowuje obie bilioteki DLL
        // Wywołujemy metodę Dispose() z NativeBackend.cs, jeżeli nasze pola nie są null
        _inputBitmap?.Dispose();

        _resultPreview.Image?.Dispose();

        _highLevelBackend?.Dispose();

        _assemblyBackend?.Dispose();

        // Wywołuje oryginalną metodę (base.) z klasy nadrzędnej Form, aby dokończyć standardowe zamykanie okna
        base.OnFormClosed(e);
    }
}