#include <windows.h>
#include <shellapi.h>
#include <shlobj.h>
#include <stdio.h>
#include <string>

unsigned long long Fingerprint(HICON icon) {
    if (!icon) return 0;
    BITMAPINFO bi{}; bi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
    bi.bmiHeader.biWidth = 32; bi.bmiHeader.biHeight = -32;
    bi.bmiHeader.biPlanes = 1; bi.bmiHeader.biBitCount = 32;
    void* bits{}; HDC dc = CreateCompatibleDC(nullptr);
    HBITMAP bitmap = CreateDIBSection(dc, &bi, DIB_RGB_COLORS, &bits, nullptr, 0);
    HGDIOBJ old = SelectObject(dc, bitmap);
    memset(bits, 0, 32*32*4); DrawIconEx(dc, 0, 0, icon, 32, 32, 0, nullptr, DI_NORMAL);
    unsigned long long hash = 14695981039346656037ULL;
    for (size_t i=0; i<32*32*4; ++i) { hash ^= ((BYTE*)bits)[i]; hash *= 1099511628211ULL; }
    SelectObject(dc, old); DeleteObject(bitmap); DeleteDC(dc); DestroyIcon(icon);
    return hash;
}

// Process-local registry redirection: never changes Explorer's real configuration.
int wmain(int argc, wchar_t** argv) {
    if (argc < 4) return 2;
    if (wcscmp(argv[1], L"real") == 0) {
        CoInitialize(nullptr);
        SHFILEINFOW realInfo{};
        auto result = SHGetFileInfoW(argv[3], 0, &realInfo, sizeof(realInfo), SHGFI_ICON | SHGFI_LARGEICON);
        wprintf(L"Real file: %llu index=%d hash=%llx\n", (unsigned long long)result, realInfo.iIcon, Fingerprint(realInfo.hIcon));
        realInfo = {};
        result = SHGetFileInfoW(argv[2], 0, &realInfo, sizeof(realInfo), SHGFI_ICON | SHGFI_LARGEICON);
        wprintf(L"Reference: %llu index=%d hash=%llx\n", (unsigned long long)result, realInfo.iIcon, Fingerprint(realInfo.hIcon));
        CoUninitialize();
        return 0;
    }
    std::wstring root = L"Software\\FileIconManagerProbe-" + std::to_wstring(GetCurrentProcessId());
    HKEY user{}, machine{}, key{}, classes{};
    if (RegCreateKeyExW(HKEY_CURRENT_USER, (root + L"\\User").c_str(), 0, nullptr, 0, KEY_ALL_ACCESS, nullptr, &user, nullptr) != ERROR_SUCCESS ||
        RegCreateKeyExW(HKEY_CURRENT_USER, (root + L"\\Machine").c_str(), 0, nullptr, 0, KEY_ALL_ACCESS, nullptr, &machine, nullptr) != ERROR_SUCCESS) return 3;
    HKEY selected = wcscmp(argv[1], L"machine") == 0 ? machine : user;
    if (wcscmp(argv[1], L"baseline") != 0) {
        RegCreateKeyExW(selected, L"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Shell Icons", 0, nullptr, 0, KEY_ALL_ACCESS, nullptr, &key, nullptr);
        std::wstring icon = std::wstring(L"\"") + argv[2] + L"\",0";
        for (const auto name : {L"0", L"1", L"2", L"3", L"4", L"7", L"8", L"9", L"10", L"11", L"16", L"29"})
            RegSetValueExW(key, name, 0, REG_SZ, (BYTE*)icon.c_str(), (DWORD)((icon.size()+1)*sizeof(wchar_t)));
        RegCloseKey(key);
        if (wcsncmp(argv[1], L"user", 4) == 0) {
            for (const auto clsid : {L"{20D04FE0-3AEA-1069-A2D8-08002B30309D}", L"{645FF040-5081-101B-9F08-00AA002F954E}",
                L"{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}", L"{59031a47-3f72-44a7-89c5-5595fe6b30ee}"}) {
                std::wstring path = std::wstring(L"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\CLSID\\") + clsid + L"\\DefaultIcon";
                RegCreateKeyExW(user, path.c_str(), 0, nullptr, 0, KEY_ALL_ACCESS, nullptr, &key, nullptr);
                for (const auto name : {L"", L"empty", L"full"}) {
                    if (wcscmp(argv[1], L"user-empty") == 0 && wcscmp(name, L"empty") != 0) continue;
                    if (wcscmp(argv[1], L"user-full") == 0 && wcscmp(name, L"full") != 0) continue;
                    RegSetValueExW(key, name, 0, REG_SZ, (BYTE*)icon.c_str(), (DWORD)((icon.size()+1)*sizeof(wchar_t)));
                }
                RegCloseKey(key);
            }
        }
    }
    if (RegOverridePredefKey(HKEY_CURRENT_USER, user) != ERROR_SUCCESS ||
        RegOverridePredefKey(HKEY_LOCAL_MACHINE, machine) != ERROR_SUCCESS) {
        RegOverridePredefKey(HKEY_CURRENT_USER, nullptr); RegOverridePredefKey(HKEY_LOCAL_MACHINE, nullptr); return 4;
    }
    RegCreateKeyExW(HKEY_LOCAL_MACHINE, L"Software\\Classes\\.exe", 0, nullptr, 0, KEY_ALL_ACCESS, nullptr, &key, nullptr);
    RegSetValueExW(key, nullptr, 0, REG_SZ, (BYTE*)L"exefile", sizeof(L"exefile")); RegCloseKey(key);
    RegCreateKeyExW(HKEY_LOCAL_MACHINE, L"Software\\Classes\\exefile\\DefaultIcon", 0, nullptr, 0, KEY_ALL_ACCESS, nullptr, &key, nullptr);
    RegSetValueExW(key, nullptr, 0, REG_SZ, (BYTE*)L"%1", sizeof(L"%1")); RegCloseKey(key);
    if (wcscmp(argv[1], L"all-exe") == 0) {
        RegCreateKeyExW(HKEY_LOCAL_MACHINE, L"Software\\Classes\\exefile\\DefaultIcon", 0, nullptr, 0, KEY_ALL_ACCESS, nullptr, &key, nullptr);
        std::wstring icon = std::wstring(L"\"") + argv[2] + L"\",0";
        RegSetValueExW(key, nullptr, 0, REG_SZ, (BYTE*)icon.c_str(), (DWORD)((icon.size()+1)*sizeof(wchar_t))); RegCloseKey(key);
    }
    // HKCR's OS-level merge does not follow redirected HKCU. For file-type tests
    // explicitly expose the test's merged Classes view. Namespace tests retain COM registrations.
    if (wcsncmp(argv[3], L"::", 2) != 0) {
        if (RegOpenKeyExW(machine, L"Software\\Classes", 0, KEY_ALL_ACCESS, &classes) != ERROR_SUCCESS ||
            RegOverridePredefKey(HKEY_CLASSES_ROOT, classes) != ERROR_SUCCESS) {
            RegOverridePredefKey(HKEY_CURRENT_USER, nullptr); RegOverridePredefKey(HKEY_LOCAL_MACHINE, nullptr); return 5;
        }
    }
    if (wcsncmp(argv[3], L"::", 2) == 0) RegOverridePredefKey(HKEY_LOCAL_MACHINE, nullptr);
    CoInitialize(nullptr);
    for (int id : {0, 1, 2, 3, 4, 7, 8, 9, 10, 11, 16, 29}) {
        SHSTOCKICONINFO si{}; si.cbSize = sizeof(si);
        HRESULT hr = SHGetStockIconInfo((SHSTOCKICONID)id, SHGSI_ICON, &si);
        wprintf(L"Stock %d: %lx %s,%d hash=%llx\n", id, hr, si.szPath, si.iIcon, Fingerprint(si.hIcon));
    }
    SHFILEINFOW info{};
    auto result = SHGetFileInfoW(argv[3], 0, &info, sizeof(info), SHGFI_ICONLOCATION);
    wprintf(L"File: %llu %s,%d\n", (unsigned long long)result, info.szDisplayName, info.iIcon);
    SHGetFileInfoW(argv[3], 0, &info, sizeof(info), SHGFI_ICON);
    wprintf(L"File icon hash=%llx\n", Fingerprint(info.hIcon));
    if (wcsncmp(argv[3], L"::", 2) == 0) {
        PIDLIST_ABSOLUTE pidl{};
        HRESULT hr = SHParseDisplayName(argv[3], nullptr, &pidl, 0, nullptr);
        info = {};
        if (SUCCEEDED(hr)) SHGetFileInfoW((LPCWSTR)pidl, 0, &info, sizeof(info), SHGFI_PIDL | SHGFI_ICON);
        wprintf(L"Namespace: %lx hash=%llx\n", hr, Fingerprint(info.hIcon));
        CoTaskMemFree(pidl);
        info = {};
        SHGetFileInfoW(argv[2], 0, &info, sizeof(info), SHGFI_ICON);
        wprintf(L"Reference hash=%llx\n", Fingerprint(info.hIcon));
    }
    CoUninitialize();
    RegOverridePredefKey(HKEY_CLASSES_ROOT, nullptr);
    RegOverridePredefKey(HKEY_CURRENT_USER, nullptr);
    RegOverridePredefKey(HKEY_LOCAL_MACHINE, nullptr);
    if (classes) RegCloseKey(classes);
    RegCloseKey(user); RegCloseKey(machine);
    RegDeleteTreeW(HKEY_CURRENT_USER, root.c_str());
    return 0;
}
