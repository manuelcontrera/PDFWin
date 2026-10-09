#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shellapi.h>
#include <shobjidl_core.h>
#include <shlwapi.h>
#include <initguid.h>
#include <vector>
#include <string>
#include <atomic>

#pragma comment(lib, "shlwapi.lib")
#pragma comment(lib, "ole32.lib")
#pragma comment(lib, "shell32.lib")
#pragma comment(lib, "advapi32.lib")
#pragma comment(lib, "user32.lib")

// {F9292F5A-1EF8-4FE1-9E2D-2BBA919518AC}
DEFINE_GUID(CLSID_PDFWinCommand,
    0xf9292f5a, 0x1ef8, 0x4fe1, 0x9e, 0x2d, 0x2b, 0xba, 0x91, 0x95, 0x18, 0xac);

static HINSTANCE g_hModule = NULL;
static std::atomic<long> g_cRefModule(0);

class PDFWinExplorerCommand : public IExplorerCommand, public IObjectWithSelection
{
public:
    PDFWinExplorerCommand() : m_cRef(1), m_pSelection(nullptr)
    {
        g_cRefModule++;
    }

    virtual ~PDFWinExplorerCommand()
    {
        if (m_pSelection)
        {
            m_pSelection->Release();
            m_pSelection = nullptr;
        }
        g_cRefModule--;
    }

    // IUnknown
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) override
    {
        static const QITAB qit[] = {
            QITABENT(PDFWinExplorerCommand, IExplorerCommand),
            QITABENT(PDFWinExplorerCommand, IObjectWithSelection),
            { 0 },
        };
        return QISearch(this, qit, riid, ppv);
    }

    IFACEMETHODIMP_(ULONG) AddRef() override
    {
        return InterlockedIncrement(&m_cRef);
    }

    IFACEMETHODIMP_(ULONG) Release() override
    {
        long cRef = InterlockedDecrement(&m_cRef);
        if (cRef == 0)
        {
            delete this;
        }
        return cRef;
    }

    // IExplorerCommand
    IFACEMETHODIMP GetTitle(IShellItemArray* psiItemArray, LPWSTR* ppszName) override
    {
        *ppszName = nullptr;

        IShellItemArray* items = psiItemArray ? psiItemArray : m_pSelection;
        bool onlyPdfs = false;

        if (items)
        {
            DWORD count = 0;
            if (SUCCEEDED(items->GetCount(&count)) && count > 0)
            {
                onlyPdfs = true;
                for (DWORD i = 0; i < count; ++i)
                {
                    IShellItem* item = nullptr;
                    if (SUCCEEDED(items->GetItemAt(i, &item)))
                    {
                        PWSTR pszPath = nullptr;
                        if (SUCCEEDED(item->GetDisplayName(SIGDN_FILESYSPATH, &pszPath)))
                        {
                            LPCWSTR ext = PathFindExtensionW(pszPath);
                            if (!ext || _wcsicmp(ext, L".pdf") != 0)
                            {
                                onlyPdfs = false;
                            }
                            CoTaskMemFree(pszPath);
                        }
                        item->Release();
                    }
                    if (!onlyPdfs) break;
                }
            }
        }

        const WCHAR* title = L"Create PDF";
        return SHStrDupW(title, ppszName);
    }

    IFACEMETHODIMP GetIcon(IShellItemArray* psiItemArray, LPWSTR* ppszIcon) override
    {
        *ppszIcon = nullptr;
        WCHAR szModule[MAX_PATH];
        if (GetModuleFileNameW(g_hModule, szModule, MAX_PATH))
        {
            WCHAR* lastSlash = wcsrchr(szModule, L'\\');
            if (lastSlash)
            {
                *(lastSlash + 1) = L'\0';
                WCHAR szExe[MAX_PATH];
                swprintf_s(szExe, MAX_PATH, L"%sPDFWin.exe,0", szModule);
                return SHStrDupW(szExe, ppszIcon);
            }
        }
        return E_FAIL;
    }

    IFACEMETHODIMP GetToolTip(IShellItemArray* psiItemArray, LPWSTR* ppszInfotip) override
    {
        *ppszInfotip = nullptr;
        return SHStrDupW(L"Create PDF", ppszInfotip);
    }

    IFACEMETHODIMP GetCanonicalName(GUID* pguidCommandName) override
    {
        *pguidCommandName = CLSID_PDFWinCommand;
        return S_OK;
    }

    IFACEMETHODIMP GetState(IShellItemArray* psiItemArray, BOOL fOkToBeSlow, EXPCMDSTATE* pCmdState) override
    {
        *pCmdState = ECS_ENABLED;
        IShellItemArray* items = psiItemArray ? psiItemArray : m_pSelection;

        if (!items)
        {
            return S_OK;
        }

        DWORD count = 0;
        if (FAILED(items->GetCount(&count)) || count == 0)
        {
            return S_OK;
        }

        // Fast check: look for at least one supported item (image, pdf, or folder)
        for (DWORD i = 0; i < count; ++i)
        {
            IShellItem* item = nullptr;
            if (SUCCEEDED(items->GetItemAt(i, &item)))
            {
                SFGAOF attribs = 0;
                if (SUCCEEDED(item->GetAttributes(SFGAO_FOLDER, &attribs)) && (attribs & SFGAO_FOLDER))
                {
                    item->Release();
                    return S_OK; // Folders are supported
                }

                PWSTR pszPath = nullptr;
                if (SUCCEEDED(item->GetDisplayName(SIGDN_FILESYSPATH, &pszPath)))
                {
                    LPCWSTR ext = PathFindExtensionW(pszPath);
                    if (ext && (_wcsicmp(ext, L".jpg") == 0 ||
                                _wcsicmp(ext, L".jpeg") == 0 ||
                                _wcsicmp(ext, L".png") == 0 ||
                                _wcsicmp(ext, L".webp") == 0 ||
                                _wcsicmp(ext, L".tif") == 0 ||
                                _wcsicmp(ext, L".tiff") == 0 ||
                                _wcsicmp(ext, L".bmp") == 0 ||
                                _wcsicmp(ext, L".pdf") == 0))
                    {
                        CoTaskMemFree(pszPath);
                        item->Release();
                        return S_OK; // Supported image/pdf
                    }
                    CoTaskMemFree(pszPath);
                }
                item->Release();
            }
        }

        *pCmdState = ECS_HIDDEN;
        return S_OK;
    }

    IFACEMETHODIMP Invoke(IShellItemArray* psiItemArray, IBindCtx* pbc) override
    {
        WCHAR szModule[MAX_PATH];
        if (!GetModuleFileNameW(g_hModule, szModule, MAX_PATH))
        {
            return E_FAIL;
        }
        WCHAR* lastSlash = wcsrchr(szModule, L'\\');
        if (!lastSlash) return E_FAIL;
        *(lastSlash + 1) = L'\0';

        WCHAR szExe[MAX_PATH];
        swprintf_s(szExe, MAX_PATH, L"%sPDFWin.exe", szModule);

        IShellItemArray* items = psiItemArray ? psiItemArray : m_pSelection;
        if (!items)
        {
            // Launch main app
            ShellExecuteW(NULL, L"open", szExe, NULL, szModule, SW_SHOWNORMAL);
            return S_OK;
        }

        DWORD count = 0;
        items->GetCount(&count);
        if (count == 0)
        {
            ShellExecuteW(NULL, L"open", szExe, NULL, szModule, SW_SHOWNORMAL);
            return S_OK;
        }

        bool isSingleFolder = false;
        if (count == 1)
        {
            IShellItem* item = nullptr;
            if (SUCCEEDED(items->GetItemAt(0, &item)))
            {
                SFGAOF attribs = 0;
                if (SUCCEEDED(item->GetAttributes(SFGAO_FOLDER, &attribs)) && (attribs & SFGAO_FOLDER))
                {
                    isSingleFolder = true;
                }
                item->Release();
            }
        }

        std::wstring cmdLine = L"\"";
        cmdLine += szExe;
        cmdLine += L"\"";

        if (isSingleFolder)
        {
            cmdLine += L" --folder";
            IShellItem* item = nullptr;
            if (SUCCEEDED(items->GetItemAt(0, &item)))
            {
                PWSTR pszPath = nullptr;
                if (SUCCEEDED(item->GetDisplayName(SIGDN_FILESYSPATH, &pszPath)))
                {
                    cmdLine += L" \"";
                    cmdLine += pszPath;
                    cmdLine += L"\"";
                    CoTaskMemFree(pszPath);
                }
                item->Release();
            }
        }
        else
        {
            cmdLine += L" --silent-create";
            for (DWORD i = 0; i < count; ++i)
            {
                IShellItem* item = nullptr;
                if (SUCCEEDED(items->GetItemAt(i, &item)))
                {
                    PWSTR pszPath = nullptr;
                    if (SUCCEEDED(item->GetDisplayName(SIGDN_FILESYSPATH, &pszPath)))
                    {
                        cmdLine += L" \"";
                        cmdLine += pszPath;
                        cmdLine += L"\"";
                        CoTaskMemFree(pszPath);
                    }
                    item->Release();
                }
            }
        }

        STARTUPINFOW si = { sizeof(si) };
        PROCESS_INFORMATION pi = { 0 };
        std::vector<WCHAR> cmdLineBuf(cmdLine.begin(), cmdLine.end());
        cmdLineBuf.push_back(L'\0');

        if (CreateProcessW(szExe, cmdLineBuf.data(), NULL, NULL, FALSE, 0, NULL, szModule, &si, &pi))
        {
            CloseHandle(pi.hProcess);
            CloseHandle(pi.hThread);
            return S_OK;
        }

        return HRESULT_FROM_WIN32(GetLastError());
    }

    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* pFlags) override
    {
        *pFlags = ECF_DEFAULT;
        return S_OK;
    }

    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** ppEnum) override
    {
        *ppEnum = nullptr;
        return E_NOTIMPL;
    }

    // IObjectWithSelection
    IFACEMETHODIMP SetSelection(IShellItemArray* psia) override
    {
        if (m_pSelection)
        {
            m_pSelection->Release();
            m_pSelection = nullptr;
        }

        if (psia)
        {
            m_pSelection = psia;
            m_pSelection->AddRef();
        }
        return S_OK;
    }

    IFACEMETHODIMP GetSelection(REFIID riid, void** ppv) override
    {
        if (m_pSelection)
        {
            return m_pSelection->QueryInterface(riid, ppv);
        }
        *ppv = nullptr;
        return E_FAIL;
    }

private:
    long m_cRef;
    IShellItemArray* m_pSelection;
};

class PDFWinClassFactory : public IClassFactory
{
public:
    PDFWinClassFactory() : m_cRef(1)
    {
        g_cRefModule++;
    }

    virtual ~PDFWinClassFactory()
    {
        g_cRefModule--;
    }

    // IUnknown
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) override
    {
        static const QITAB qit[] = {
            QITABENT(PDFWinClassFactory, IClassFactory),
            { 0 },
        };
        return QISearch(this, qit, riid, ppv);
    }

    IFACEMETHODIMP_(ULONG) AddRef() override
    {
        return InterlockedIncrement(&m_cRef);
    }

    IFACEMETHODIMP_(ULONG) Release() override
    {
        long cRef = InterlockedDecrement(&m_cRef);
        if (cRef == 0)
        {
            delete this;
        }
        return cRef;
    }

    // IClassFactory
    IFACEMETHODIMP CreateInstance(IUnknown* punkOuter, REFIID riid, void** ppv) override
    {
        *ppv = nullptr;
        if (punkOuter)
        {
            return CLASS_E_NOAGGREGATION;
        }

        PDFWinExplorerCommand* pCmd = new (std::nothrow) PDFWinExplorerCommand();
        if (!pCmd)
        {
            return E_OUTOFMEMORY;
        }

        HRESULT hr = pCmd->QueryInterface(riid, ppv);
        pCmd->Release();
        return hr;
    }

    IFACEMETHODIMP LockServer(BOOL fLock) override
    {
        if (fLock)
        {
            g_cRefModule++;
        }
        else
        {
            g_cRefModule--;
        }
        return S_OK;
    }

private:
    long m_cRef;
};

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
    if (ul_reason_for_call == DLL_PROCESS_ATTACH)
    {
        g_hModule = hModule;
        DisableThreadLibraryCalls(hModule);
    }
    return TRUE;
}

STDAPI DllGetClassObject(REFCLSID rclsid, REFIID riid, void** ppv)
{
    *ppv = nullptr;
    if (IsEqualCLSID(rclsid, CLSID_PDFWinCommand))
    {
        PDFWinClassFactory* pFactory = new (std::nothrow) PDFWinClassFactory();
        if (!pFactory)
        {
            return E_OUTOFMEMORY;
        }
        HRESULT hr = pFactory->QueryInterface(riid, ppv);
        pFactory->Release();
        return hr;
    }
    return CLASS_E_CLASSNOTAVAILABLE;
}

STDAPI DllCanUnloadNow(void)
{
    return (g_cRefModule == 0) ? S_OK : S_FALSE;
}
