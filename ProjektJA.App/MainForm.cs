// Projekt: JA — przetwarzanie obrazu (temat do zatwierdzenia)
// Algorytm: wspólny interfejs użytkownika; kernel zostanie podłączony później
// Autor: [uzupełnić]
// Data: 2026-10-10
// Semestr/Rok akademicki: semestr 5 / 2026-2027
// Wersja: 0.1.0 — szkielet GUI
// Historia zmian: 0.1.0 — układ, wybór DLL, wątków i podgląd obrazu

using System.Drawing;
using System.Windows.Forms;

namespace ProjektJA.App;

/// <summary>
/// Główne okno aplikacji z elementami wspólnymi dla obu proponowanych algorytmów.
/// </summary>
internal sealed class MainForm : Form
{
    /// <summary>Background color used for the resizable window surface.</summary>
    private static readonly Color PageColor = Color.FromArgb(244, 247, 251);
    /// <summary>Primary text color used across the window.</summary>
    private static readonly Color InkColor = Color.FromArgb(30, 41, 59);
    /// <summary>Secondary text color used for helper text and captions.</summary>
    private static readonly Color MutedColor = Color.FromArgb(100, 116, 139);
    /// <summary>Accent color used to identify the selected backend.</summary>
    private static readonly Color AccentColor = Color.FromArgb(37, 99, 235);

    /// <summary>Radio option for the C++ high-level library.</summary>
    private readonly RadioButton _highLevelOption = new() { Text = "C++ — język wysokiego poziomu", AutoSize = true };
    /// <summary>Radio option for the x64 assembly library.</summary>
    private readonly RadioButton _assemblyOption = new() { Text = "ASM x64 — biblioteka asemblerowa", AutoSize = true };
    /// <summary>Slider for choosing the worker-thread count.</summary>
    private readonly TrackBar _threadSlider = new() { Minimum = 1, Maximum = 64, TickFrequency = 4, SmallChange = 1, LargeChange = 8, Height = 44 };
    /// <summary>Numeric thread-count input synchronized with the slider.</summary>
    private readonly NumericUpDown _threadInput = new() { Minimum = 1, Maximum = 64, Width = 96, TextAlign = HorizontalAlignment.Center };
    /// <summary>Displays the detected logical processor count.</summary>
    private readonly Label _threadSummary = new() { AutoSize = true, ForeColor = MutedColor };
    /// <summary>Shows the active library name or a loading problem.</summary>
    private readonly Label _selectedBackend = new() { AutoSize = true, ForeColor = AccentColor, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
    /// <summary>Reserved display for the algorithm execution time.</summary>
    private readonly Label _timeValue = new() { Text = "—", AutoSize = true, ForeColor = InkColor, Font = new Font("Segoe UI", 27F, FontStyle.Bold) };
    /// <summary>Explains that timing is inactive until an algorithm is implemented.</summary>
    private readonly Label _timeNote = new() { Text = "Pomiar włączy się po dodaniu algorytmu.", AutoSize = true, ForeColor = MutedColor, MaximumSize = new Size(280, 0) };
    /// <summary>Shows whether both native libraries are available.</summary>
    private readonly Label _statusPill = new() { AutoSize = true, Padding = new Padding(10, 7, 10, 7), BackColor = Color.FromArgb(226, 232, 240), ForeColor = InkColor, Text = "Szkielet aplikacji" };
    /// <summary>Shows the selected input filename.</summary>
    private readonly Label _imageStatus = new() { AutoSize = true, ForeColor = MutedColor };
    /// <summary>Opens the image picker for the source preview.</summary>
    private readonly Button _loadImageButton = new() { Text = "Wczytaj obraz", Width = 280, Height = 42 };
    /// <summary>Reserved for the future algorithm execution action.</summary>
    private readonly Button _runButton = new() { Text = "Uruchom algorytm", Width = 280, Height = 42, Enabled = false };
    /// <summary>Saves the processed output bitmap to disk.</summary>
    private readonly Button _saveButton = new() { Text = "Zapisz wynik", Width = 280, Height = 42, Enabled = false };
    /// <summary>Runs automated multi-threaded benchmark for C++ and ASM and exports CSV.</summary>
    private readonly Button _benchmarkButton = new() { Text = "Benchmark (CSV)", Width = 280, Height = 42, Enabled = false };
    /// <summary>Displays the loaded source image.</summary>
    private readonly PictureBox _sourcePreview = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White };
    /// <summary>Displays the filtered result after the kernel is implemented.</summary>
    private readonly PictureBox _resultPreview = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White };
    /// <summary>Placeholder shown before an input image is loaded.</summary>
    private readonly Label _sourcePlaceholder = new() { Text = "Wczytaj własny obraz, aby zobaczyć podgląd.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = MutedColor, Padding = new Padding(24) };
    /// <summary>Placeholder shown until a filter result exists.</summary>
    private readonly Label _resultPlaceholder = new() { Text = "Wynik pojawi się po podłączeniu algorytmu.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = MutedColor, Padding = new Padding(24) };

    /// <summary>Handle to the loaded C++ DLL and its exported functions.</summary>
    private NativeBackend? _highLevelBackend;
    /// <summary>Handle to the loaded ASM DLL and its exported functions.</summary>
    private NativeBackend? _assemblyBackend;
    /// <summary>Detached bitmap held for the source preview.</summary>
    private Bitmap? _inputBitmap;
    /// <summary>Prevents recursive value-change handling between the two thread controls.</summary>
    private bool _syncingThreadControls;

    /// <summary>
    /// Tworzy responsywne okno, inicjalizuje kontrolki i ładuje obie biblioteki DLL.
    /// </summary>
    public MainForm()
    {
        Text = "Projekt JA — przetwarzanie obrazu";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(980, 650);
        ClientSize = new Size(1220, 790);
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

    /// <summary>
    /// Buduje układ Dock/Anchor, który rozszerza się wraz z oknem i skalą DPI.
    /// </summary>
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

    /// <summary>
    /// Builds the title area and its compact project status badge.
    /// </summary>
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
            Text = "Przetwarzanie obrazu • wspólny szkielet aplikacji",
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

    /// <summary>
    /// Builds the left control card with DLL choice, thread count, timer placeholder and image actions.
    /// </summary>
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
            Height = 112,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(12),
            Margin = new Padding(0, 8, 0, 4)
        };
        _timeValue.Location = new Point(12, 8);
        _timeNote.Location = new Point(14, 60);
        timerBox.Controls.Add(_timeValue);
        timerBox.Controls.Add(_timeNote);
        flow.Controls.Add(timerBox);

        flow.Controls.Add(SectionTitle("Dane wejściowe"));
        _loadImageButton.FlatStyle = FlatStyle.Flat;
        _loadImageButton.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _loadImageButton.BackColor = Color.White;
        _loadImageButton.ForeColor = InkColor;
        _loadImageButton.Margin = new Padding(0, 8, 0, 0);
        flow.Controls.Add(_loadImageButton);
        _imageStatus.Margin = new Padding(0, 1, 0, 4);
        flow.Controls.Add(_imageStatus);

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

    /// <summary>
    /// Builds the input and output preview cards plus a short ABI status line.
    /// </summary>
    private Control BuildPreviewArea()
    {
        var area = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = PageColor,
            Padding = new Padding(12, 0, 0, 0)
        };
        area.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        area.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        area.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        area.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));

        area.Controls.Add(BuildImageCard("OBRAZ WEJŚCIOWY", _sourcePreview, _sourcePlaceholder), 0, 0);
        area.Controls.Add(BuildImageCard("WYNIK", _resultPreview, _resultPlaceholder), 1, 0);

        var footer = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = MutedColor,
            Text = "Umowa bibliotek: BGR24 • wspólne eksporty C ABI • liczba wątków przekazywana z GUI"
        };
        area.Controls.Add(footer, 0, 1);
        area.SetColumnSpan(footer, 2);
        return area;
    }

    /// <summary>
    /// Creates a white card with a title and a scalable image preview.
    /// </summary>
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

    /// <summary>
    /// Creates a reusable padded white panel for the main content cards.
    /// </summary>
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

    /// <summary>
    /// Creates a section heading in the control panel.
    /// </summary>
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

    /// <summary>
    /// Initializes the slider and numeric box with the detected logical processor count.
    /// </summary>
    private void InitializeThreadCount()
    {
        var detected = Math.Clamp(Environment.ProcessorCount, 1, 64);
        _threadSlider.Value = detected;
        _threadInput.Value = detected;
    }

    /// <summary>
    /// Loads both DLLs from the application folder; the app stays open if a DLL is missing.
    /// </summary>
    private void LoadBackends()
    {
        try
        {
            _highLevelBackend = NativeBackend.Load("ProjektJA.HighLevel.dll");
        }
        catch (Exception ex)
        {
            _selectedBackend.Text = $"C++: nie udało się wczytać ({ex.Message})";
        }

        try
        {
            _assemblyBackend = NativeBackend.Load("ProjektJA.Assembly.dll");
        }
        catch (Exception ex)
        {
            if (_highLevelBackend is null)
            {
                _selectedBackend.Text = $"ASM: nie udało się wczytać ({ex.Message})";
            }
        }

        var bothLoaded = _highLevelBackend is not null && _assemblyBackend is not null;
        _statusPill.Text = bothLoaded ? "Obie DLL wczytane" : "Brak biblioteki DLL";
        _statusPill.BackColor = bothLoaded
            ? Color.FromArgb(220, 252, 231)
            : Color.FromArgb(254, 226, 226);
        _statusPill.ForeColor = bothLoaded
            ? Color.FromArgb(22, 101, 52)
            : Color.FromArgb(153, 27, 27);
    }

    /// <summary>
    /// Updates the displayed library name when the selected radio button changes.
    /// </summary>
    private void UpdateSelectedBackend()
    {
        var backend = _assemblyOption.Checked ? _assemblyBackend : _highLevelBackend;
        var label = _assemblyOption.Checked ? "ASM" : "C++";
        _selectedBackend.Text = backend is null
            ? $"{label}: biblioteka niedostępna"
            : $"Aktywna biblioteka: {backend.Name}";
    }

    /// <summary>
    /// Keeps the slider and numeric field synchronized without recursive updates.
    /// </summary>
    private void SynchronizeThreadCount(int value)
    {
        if (_syncingThreadControls)
        {
            return;
        }

        _syncingThreadControls = true;
        _threadSlider.Value = Math.Clamp(value, _threadSlider.Minimum, _threadSlider.Maximum);
        _threadInput.Value = Math.Clamp(value, (int)_threadInput.Minimum, (int)_threadInput.Maximum);
        _syncingThreadControls = false;
    }

    /// <summary>
    /// Opens an image and shows a detached bitmap preview without locking the source file.
    /// </summary>
    private void LoadImage()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Wybierz obraz do podglądu",
            Filter = "Pliki obrazów|*.bmp;*.png;*.jpg;*.jpeg;*.tif;*.tiff|Wszystkie pliki|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        string extension = Path.GetExtension(dialog.FileName).ToLower();
        if (extension != ".jpg" && extension != ".png" && extension != ".bmp")
        {
            MessageBox.Show(this, "Ten format pliku nie jest wspierany przez wbudowany silnik. Użyj standardowego JPG, PNG lub BMP.",
                "Brak wsparcia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            using var loaded = Image.FromFile(dialog.FileName);
            var detachedBitmap = new Bitmap(loaded);
            var previousBitmap = _inputBitmap;
            _inputBitmap = detachedBitmap;
            _sourcePreview.Image = detachedBitmap;
            _sourcePlaceholder.Visible = false;
            _imageStatus.Text = Path.GetFileName(dialog.FileName);
            _timeValue.Text = "—";
            _timeNote.Text = "Wczytany obraz jest na razie tylko podglądem.";
            _runButton.Enabled = true;
            _runButton.BackColor = AccentColor;
            _runButton.ForeColor = Color.White;

            if (_highLevelBackend is not null && _assemblyBackend is not null)
            {
                _benchmarkButton.Enabled = true;
                _benchmarkButton.BackColor = Color.White;
                _benchmarkButton.ForeColor = InkColor;
            }

            // Auto-reset prawego okna z wynikiem po wczytaniu nowego obrazu
            var previousResult = _resultPreview.Image;
            _resultPreview.Image = null;
            _resultPlaceholder.Visible = true;
            previousResult?.Dispose();

            // Zablokowanie przycisku zapisu do czasu wygenerowania nowego wyniku
            _saveButton.Enabled = false;
            _saveButton.BackColor = Color.FromArgb(226, 232, 240);
            _saveButton.ForeColor = MutedColor;

            previousBitmap?.Dispose();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Nie udało się wczytać obrazu.\n\n{ex.Message}",
                "Błąd wczytywania",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Releases the preview bitmap and native modules when the window closes.
    /// </summary>
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _inputBitmap?.Dispose();
        _highLevelBackend?.Dispose();
        _assemblyBackend?.Dispose();
        base.OnFormClosed(e);
        _resultPreview.Image?.Dispose();
    }

    /// <summary>
    /// Uruchamia wybraną bibliotekę DLL, przekazuje jej zablokowaną pamięć obrazu i mierzy czas.
    /// </summary>
    private async void RunAlgorithm()
    {
        if (_inputBitmap == null) return;
        var backend = _assemblyOption.Checked ? _assemblyBackend : _highLevelBackend;
        if (backend == null) return;

        int threads = (int)_threadInput.Value;

        using Bitmap normalizedInput = _inputBitmap.Clone(
            new Rectangle(0, 0, _inputBitmap.Width, _inputBitmap.Height),
            System.Drawing.Imaging.PixelFormat.Format24bppRgb);

        // Kopiujemy oryginalny obraz na wyjście, aby zachować brzegowe piksele, których maska 3x3 nie może dotknąć
        Bitmap outputBitmap = new Bitmap(normalizedInput.Width, normalizedInput.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(outputBitmap))
        {
            g.DrawImageUnscaled(normalizedInput, 0, 0);
        }

        var rect = new Rectangle(0, 0, normalizedInput.Width, normalizedInput.Height);
        var inData = normalizedInput.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, normalizedInput.PixelFormat);
        var outData = outputBitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.WriteOnly, outputBitmap.PixelFormat);

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Podział wysokości obrazu na poprawne paski przydzielane wątkom
            int usableHeight = inData.Height - 2;
            int rowsPerThread = usableHeight / threads;
            int remainder = usableHeight % threads;

            Task[] tasks = new Task[threads];
            int currentY = 1;

            for (int i = 0; i < threads; i++)
            {
                int startY = currentY;
                int endY = startY + rowsPerThread + (i < remainder ? 1 : 0);
                currentY = endY;

                tasks[i] = Task.Run(() =>
                {
                    // DLL otrzymuje teraz parametry: in, out, szerokość, stride, startY, endY
                    int status = backend.Process(inData.Scan0, outData.Scan0, inData.Width, inData.Stride, startY, endY);
                    if (status != 0) throw new Exception($"Zewnętrzny kod błędu: {status}");
                });
            }

            await Task.WhenAll(tasks); // Płynna synchronizacja wykonania na rdzeniach
            sw.Stop();

            _timeValue.Text = $"{sw.Elapsed.TotalMilliseconds:F2} ms";
            _timeNote.Text = $"Czas algorytmu dla rozdzielczości {inData.Width}x{inData.Height} ({threads} wątków).";

            var oldResult = _resultPreview.Image;
            _resultPreview.Image = outputBitmap;
            _resultPlaceholder.Visible = false;
            oldResult?.Dispose();
            _saveButton.Enabled = true;
            _saveButton.BackColor = Color.White;
            _saveButton.ForeColor = InkColor;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Błąd wykonania:\n\n{ex.InnerException?.Message ?? ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            outputBitmap.Dispose();
        }
        finally
        {
            normalizedInput.UnlockBits(inData);
            outputBitmap.UnlockBits(outData);
        }
    }

    /// <summary>
    /// Zapisuje przetworzony obraz wynikowy do pliku wybranego przez użytkownika.
    /// </summary>
    private void SaveImage()
    {
        if (_resultPreview.Image is null) return;

        using var dialog = new SaveFileDialog
        {
            Title = "Zapisz przetworzony obraz",
            Filter = "Obraz PNG (*.png)|*.png|Mapa bitowa BMP (*.bmp)|*.bmp|Obraz JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg",
            DefaultExt = "png",
            FileName = "wynik_gauss"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();
            var format = ext switch
            {
                ".bmp" => System.Drawing.Imaging.ImageFormat.Bmp,
                ".jpg" or ".jpeg" => System.Drawing.Imaging.ImageFormat.Jpeg,
                _ => System.Drawing.Imaging.ImageFormat.Png
            };

            _resultPreview.Image.Save(dialog.FileName, format);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Nie udało się zapisać obrazu.\n\n{ex.Message}",
                "Błąd zapisu",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Przeprowadza serię pomiarów (C++ vs ASM) dla różnych liczb wątków i zapisuje wyniki do pliku CSV.
    /// </summary>
    private async void RunBenchmark()
    {
        if (_inputBitmap is null || _highLevelBackend is null || _assemblyBackend is null) return;

        using var dialog = new SaveFileDialog
        {
            Title = "Zapisz wyniki benchmarku do CSV",
            Filter = "Plik CSV (*.csv)|*.csv",
            DefaultExt = "csv",
            FileName = $"benchmark_{_inputBitmap.Width}x{_inputBitmap.Height}.csv"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        int maxThreads = (int)_threadInput.Maximum; // Domyślnie 64
        const int iterations = 10;      // 10 iteracji dla średniego czasu

        // Zablokowanie przycisków na czas testu
        _runButton.Enabled = false;
        _loadImageButton.Enabled = false;
        _benchmarkButton.Enabled = false;

        using Bitmap normalizedInput = _inputBitmap.Clone(
            new Rectangle(0, 0, _inputBitmap.Width, _inputBitmap.Height),
            System.Drawing.Imaging.PixelFormat.Format24bppRgb);

        using Bitmap outputBitmap = new Bitmap(normalizedInput.Width, normalizedInput.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

        var rect = new Rectangle(0, 0, normalizedInput.Width, normalizedInput.Height);
        var inData = normalizedInput.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, normalizedInput.PixelFormat);
        var outData = outputBitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.WriteOnly, outputBitmap.PixelFormat);

        var csvBuilder = new System.Text.StringBuilder();
        csvBuilder.AppendLine("Liczba watkow;Sredni czas C++ [ms];Sredni czas ASM [ms];Przyspieszenie (C++/ASM)");

        var plCulture = System.Globalization.CultureInfo.GetCultureInfo("pl-PL");

        try
        {
            for (int threads = 1; threads <= maxThreads; threads++)
            {
                // Pomiar dla C++
                double cppAvg = await MeasureBackendAsync(_highLevelBackend, inData, outData, threads, maxThreads, iterations, "C++");
                // Pomiar dla ASM
                double asmAvg = await MeasureBackendAsync(_assemblyBackend, inData, outData, threads, maxThreads, iterations, "ASM");

                double speedup = asmAvg > 0 ? cppAvg / asmAvg : 0;

                csvBuilder.AppendLine(string.Format(plCulture, "{0};{1:F4};{2:F4};{3:F2}", threads, cppAvg, asmAvg, speedup));
            }

            File.WriteAllText(dialog.FileName, csvBuilder.ToString(), System.Text.Encoding.UTF8);
            _timeNote.Text = "Benchmark zakończony pomyślnie (1–64 wątków). Zapisano plik CSV.";
            MessageBox.Show(this, $"Zakończono pełny test (1–{maxThreads} wątków)!\nWyniki zapisano w:\n{dialog.FileName}", "Benchmark gotowy", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Błąd podczas benchmarku:\n\n{ex.InnerException?.Message ?? ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            normalizedInput.UnlockBits(inData);
            outputBitmap.UnlockBits(outData);

            _runButton.Enabled = true;
            _loadImageButton.Enabled = true;
            _benchmarkButton.Enabled = true;
        }
    }

    /// <summary>
    /// Pomocnicza metoda wykonująca rozgrzewkę oraz N pomiarów dla wybranego backendu i liczby wątków.
    /// </summary>
    private async Task<double> MeasureBackendAsync(
        NativeBackend backend,
        System.Drawing.Imaging.BitmapData inData,
        System.Drawing.Imaging.BitmapData outData,
        int threads,
        int maxThreads,
        int iterations,
        string label)
    {
        _timeNote.Text = $"Benchmark ({label}): testowanie {threads}/{maxThreads} wątków ({iterations} prób)...";

        int usableHeight = inData.Height - 2;
        int rowsPerThread = usableHeight / threads;
        int remainder = usableHeight % threads;

        Task RunSinglePassAsync()
        {
            Task[] tasks = new Task[threads];
            int currentY = 1;

            for (int t = 0; t < threads; t++)
            {
                int startY = currentY;
                int endY = startY + rowsPerThread + (t < remainder ? 1 : 0);
                currentY = endY;

                tasks[t] = Task.Run(() =>
                {
                    if (startY < endY)
                    {
                        int status = backend.Process(inData.Scan0, outData.Scan0, inData.Width, inData.Stride, startY, endY);
                        if (status != 0) throw new Exception($"Kod błędu DLL: {status}");
                    }
                });
            }
            return Task.WhenAll(tasks);
        }

        // 1. Przebieg rozgrzewkowy (Warm-up)
        await RunSinglePassAsync();

        // 2. Właściwe pomiary
        double totalMs = 0;
        for (int i = 1; i <= iterations; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await RunSinglePassAsync();
            sw.Stop();
            totalMs += sw.Elapsed.TotalMilliseconds;
        }

        double avgMs = totalMs / iterations;
        _timeValue.Text = $"{avgMs:F2} ms";
        return avgMs;
    }
}