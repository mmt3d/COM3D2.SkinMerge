@echo off

set "TMPDIR=%SystemRoot%\Temp\SkinMerge_docs"
mkdir "%TMPDIR%\docs"
powershell -Command ^
    "Get-Content README.md | "^
    "Select-Object -Skip (Select-String -Path README.md -Pattern '<!-- content begin -->' | "^
    "Select-Object -First 1).LineNumber | "^
    "Set-Content -Encoding UTF8 \"%TMPDIR%\README.md\""
xcopy /ye docs "%TMPDIR%\docs"
pushd "%TMPDIR%"
pandoc README.md -o README.pdf ^
    --pdf-engine=wkhtmltopdf -c docs\pdf_style.css ^
    --pdf-engine-opt="--margin-top" --pdf-engine-opt="15mm" ^
    --pdf-engine-opt="--margin-bottom" --pdf-engine-opt="15mm" ^
    --pdf-engine-opt="--margin-left" --pdf-engine-opt="20mm" ^
    --pdf-engine-opt="--margin-right" --pdf-engine-opt="20mm"
popd
copy "%TMPDIR%\README.pdf" .\docs\README.pdf
rmdir /s /q "%TMPDIR%"
