// Przestrzeñ nazw odpowiedzialna za komunikacjê z kodem natywnym (.asm)
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ProjektJA.App;

// internal sealed class : internal - oznacza, ¿e klasa jest widoczna tylko wewn¹trz projektu GUI, a sealed blokuje po niej dziedziczenie (odpowiednik final)
//
// Czym jest : IDisposable?
// To implementacja interfejsu IDisposable (takiego jak w Java - czyli klasa wirutalna z samymi metodami)
// Ma tylko jedno wymaganie - klasa implementuj¹ca ten interfejs musi posiadaæ publiczn¹ metodê public void Dispose()
//
// A po co nam to?
// W C# zwyk³e zmienne i tablice sprz¹ta automatycznie Garbage Collector (GC). Jednak GC nie ma pojêcia o istnieniu zewnêtrznych bibliotek .dll za³adowanych przez system Windows.
// Dopisuj¹c : IDisposable, oznaczamy nasz¹ klasê w systemie .NET: „Uwaga, ta klasa trzyma w rêku zasób spoza œwiata C# (wskaŸnik w RAM-ie). Kiedy skoñczycie z niej korzystaæ, wywo³ajcie Dispose(), ¿eby zwolniæ pamiêæ
internal sealed class NativeBackend : IDisposable
{
    // W C# nie dzielimy klas na bloki np. public: ani private:
    // S³owa te dopisujemy przed ka¿d¹ zmienn¹ i funkcj¹ z osobna

    // W C# napisy w nawiasach kwadratowych [...] nad funkcj¹ lub klas¹ to tzw. Atrybuty – czyli instrukcje dla samego kompilatora mówi¹ce, jak ma wygenerowaæ kod maszynowy pod mask¹.
    //
    // UnmanagedFunctionPointer
    // W C# kod dzieli siê na Managed (zarz¹dzany przez œrodowisko .NET) oraz Unmanaged (niezarz¹dzany – czysty kod maszynowy z C++ lub Asemblera).
    // Ten atrybut mówi kompilatorowi: „Ten wskaŸnik na funkcjê nie prowadzi do metody C#, tylko wychodzi na zewn¹trz – do natywnej biblioteki DLL”.
    //
    // (CallingConvention.Cdecl)
    // W nawiasie podajemy konwencjê wywo³ania.
    // Mówimy kompilatorowi C#: „Kiedy bêdziesz wywo³ywaæ tê funkcjê, przygotuj rejestry RCX, RDX, R8, R9 oraz Shadow Space na stosie dok³adnie wed³ug standardu jêzyka C / x64 ABI.
    // delegate to taki jakby typedef - czyli znacznie uproszczona sk³adnia na normalnie nieprzyjemny typ zmiennej
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint GetBackendNameDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int ProcessImageDelegate(nint inBuffer, nint outBuffer, int width, int stride, int startY, int endY, int maskSize);

    // Uchwyt do za³adowanej biblioteki dll
    // readonly to odpowiednik const w C++
    private readonly nint libraryHandle;

    // Gotowy do wywo³ania delegat wskazuj¹cy na funkcjê przetwarzaj¹c¹ obraz
    // Delegata dla GetBackendName nie musimy zapisywaæ w polu klasy, bo wywo³amy go tylko raz podczas ³adowania biblioteki
    private readonly ProcessImageDelegate processImage;

    // Publiczna w³aœciwoœæ read-only przechowuj¹ca nazwê backendu
    // W C# zamiast pisaæ prywatne pole i osobny, publiczny getter, pisze siê jedn¹ linijkê z { get; }
    // Dzia³a to jak zmienna, któr¹ ka¿dy z zewn¹trz mo¿e odczytaæ, ale tylko konstruktor mo¿e ustawiæ
    //
    // Dlaczego to jest takie dziwne? Skoro 'setting" jest private, to dlaczego modyfikator dostêpu jest public?
    // Bo twórcy C# uznali sobie tak:
    // S³owo public z przodu okreœla widocznoœæ ca³ej w³aœciwoœci dla œwiata zewnêtrznego
    // W klamrach { } wymieniamy, jakie operacje s¹ dozwolone (w naszym przypadku get;)
    // A ca³a reszta operacji, które nie zosta³y w klamrach wymienione, NIE ISTNIEJE dla œwiata zewnêtreznego (m.in. set;)
    public string Name { get; }

    // Konstruktor naszej klasy, który otrzyma uchwyt do u¿ywanej biblioteki dll, delegata dla odpowiedniej funkcji w bibliotece i sam¹ nazwê biblioteki (High level albo ASM),
    // a nastêpnie przypisze je odpowiednim polom w obiekcie naszej klasy
    private NativeBackend(nint libraryHandle, ProcessImageDelegate processImage, string name)
    {
        this.libraryHandle = libraryHandle;
        this.processImage = processImage;
        Name = name;
    }

    // Metoda ta otwiera plik .dll, wyci¹ga z niego adresy naszych dwóch funkcji (JA_GetBackenName i JA_Process_Image), zamienia je na wskaŸniki na funkcje
    //
    // Czym jest wskaŸnik na funkcje?
    // To adres w pamiêci RAM na pierwsz¹ instrukcjê danej funkcji.
    // Dla procesora wywo³anie funkcji to po prostu skok (CALL jakiœ_adres) pod ten adres w RAM-ie.
    public static NativeBackend Load(string fileName)
    {
        // Sklejamy œcie¿kê folderu z plikiem .exe oraz nazwê pliku .dll
        //
        // var to odpowiednik auto z C++
        //
        // Co oznacza ta sk³adnia wewn¹trz .Combine()?
        // W bezpieczny sposób skleja nam ona œcie¿kê folderu, w którym uruchomiony jest .exe (AppContext.BaseDirectory) z fileName
        // Dziêki temu unikamy sytuacji, w której Windows zacz¹³by szukaæ pliku po samej nazwie (NativeLibrary.Load("LowLevel.dll") w tzw. "bie¿¹cym katalogu roboczym",
        // a w efekcie go nie znaleŸæ i zwróciæ b³¹d
        var fullPath = Path.Combine(AppContext.BaseDirectory, fileName);

        // Odpowiednik LoadLibrary z C++ - wczytuje DLL do pamiêci RAM
        //
        // Dlaczego takie coœ?
        // Procesor nie potrafi wykonywaæ kodu prosto z dysku SSD.
        // Load ka¿e systemowi Windows skopiowaæ ca³¹ zawartoœæ pliku .dll (zarówno sta³e z .const, jak i rozkazy z .code)
        // z dysku (pod adresem fullPath) do pamiêci RAM i zwraca handle – czyli adres w pamiêci RAM, pod którym ten plik zosta³ rozpakowany
        var handle = NativeLibrary.Load(fullPath);

        try
        {
            // Szukanie adresów naszych funkcji w DLL
            //
            // Dzia³a to w nastêpuj¹cy sposób:
            // Plik .dll ma na swoim pocz¹tku specjaln¹ tablicê, w której zapisane s¹ nazwy funkcji oznaczonych przez nas w ASM jako PUBLIC (lub w C++ jako extern "C")
            // "JA_GetBackendName" le¿y 0x1020 bajtów od pocz¹tku biblioteki.
            // "JA_ProcessImage" le¿y 0x1050 bajtów od pocz¹tku biblioteki.
            // GetExport zagl¹da do tej ksi¹¿ki tablicy w RAM-ie, dodaje offset do adresu handle i zwraca dok³adny adres pamiêci RAM (nint), pod którym zaczyna siê dana funkcja
            var getNamePointer = NativeLibrary.GetExport(handle, "JA_GetBackendName");
            var processPointer = NativeLibrary.GetExport(handle, "JA_ProcessImage");

            // Zmieniamy surowy adres nint na delegat, który mo¿na wywo³aæ
            // 
            // Dzia³a to tak:
            // Marshal.GetDelegateForFunctionPointer<>() bierze surowy adres pamiêci i pakuje go w wykonywaln¹ "kapsu³ê" (delegate)
            // W <> podajemy typ szablonu - jaki wzorzec funkcji chcemy ubraæ nasz adres
            // W () podajemy argument - surowy adres pamiêci
            var getName = Marshal.GetDelegateForFunctionPointer<GetBackendNameDelegate>(getNamePointer);
            var process = Marshal.GetDelegateForFunctionPointer<ProcessImageDelegate>(processPointer);

            // Wywo³ujemy funkcjê JA_GetBackendName -> w namePointer l¹duje adres etykiety GetBackendName (zapisanej w RAX przez JA_GetBackendName)
            var namePointer = getName();

            // T³umaczymy adres C-stringa (const char*) na C#-owego stringa
            //
            // Dlaczego const char*?
            // Bo taki jest zapis string'a - jest to adres pierwszej literki naszego ci¹gu znaków
            //
            // ?? to operator oznaczaj¹cy - jeœli wynik po lewej jest null, u¿yj wartoœci po prawej
            var name = Marshal.PtrToStringUTF8(namePointer) ?? fileName;

            // Static Factory Method - wzorzec projektowy
            //
            // Jak to dzia³a?
            // Metoda Load() jest statyczna - nie potrzebuje ona obiektu klasy NativeBackend do dzia³ania
            // Poniewa¿ metoda ta le¿y wewn¹trz klasy NativeBackend, ma prawo korzystaæ z prywatnego konstruktora i tym samym zwróciæ gotowy, skonfigurowany obiekt
            return new NativeBackend(handle, process, name);
        }

        catch
        {
            // W C# metoda NativeLibrary.GetExport(handle, "JA_ProcessImage") dzia³a tak - jeœli w pliku .dll nie znajdzie funkcji o takiej nazwie, nie zwraca zera,
            // tylko natychmiast rzuca wyj¹tek (Exception) EntryPointNotFoundException i przerywa wykonywanie bloku try.
            //
            // Gdyby u¿yæ if'a zamiast try/catch, w momencie rzucania wyj¹tku program natychmiast wyskoczy³by z funkcji Load, a za³adowana wy¿ej biblioteka (var handle = NativeLibrary.Load(...))
            // zosta³aby na zawsze „osierocona” w pamiêci RAM (wyciek pamiêci)
            // 
            // Blok catch ³apie ten wyj¹tek w locie, sprz¹ta pamiêæ RAM instrukcj¹ NativeLibrary.Free(handle);,
            // a nastêpnie instrukcj¹ throw; pozwala wyj¹tkowi lecieæ dalej (np. ¿eby okienko GUI mog³o wyœwietliæ komunikat b³êdu)
            NativeLibrary.Free(handle);
            throw;
        }
    }

    public int Process(nint sourceBgr24, nint destinationBgr24, int width, int strideBytes, int startY, int endY, int maskSize)
    {
        return processImage(sourceBgr24, destinationBgr24, width, strideBytes, startY, endY, maskSize);
    }

    public void Dispose()
    {
        NativeLibrary.Free(libraryHandle);
    }
}