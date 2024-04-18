# Barotrauma-Autofill-Tags

Automatically generate the Barotrauma wiki page [Autofill Tags](https://barotraumagame.com/wiki/Autofill_Tags).

## Installation

Download the [latest release](https://github.com/Jlobblet/Barotrauma-Autofill-Tags/releases/latest).


## Configuration

Edit `app.config` to change the following settings:

| Configuration Key             | Description                                                                                                      |
|-------------------------------|------------------------------------------------------------------------------------------------------------------|
| `BarotraumaLocation`          | The root directory where Barotrauma is installed.                                                                |
| `Version`                     | The version of Barotrauma to include in the output.                                                              |
| `OutputLocation` (optional)   | Where to put the output files. Defaults to the working directory.                                                |
| `SummaryLocation` (optional)  | The location of the summary, which contains the text used at the start of the article. Defaults to `summary.txt` |
| `TemplateLocation` (optional) | The location of template, which lists tags in order and with headings. Defaults to `template.txt`.               |

Additionally, default versions of `summary.txt` and `template.txt` are included, but they are not guaranteed to be up to date.
