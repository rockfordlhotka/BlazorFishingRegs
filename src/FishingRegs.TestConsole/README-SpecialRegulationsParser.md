# Special Regulations Parser

## Overview

This parser extracts fishing regulation data from the `special-regulations.txt` file located in the `/data` folder of the solution.

## Features

- Parses lake names, counties, and regulation text from the special regulations file
- Extracts species-specific regulations including:
  - Daily and possession limits
  - Size restrictions (minimum, maximum, protected slots)
  - Catch-and-release regulations
  - Regulation types
- Handles compound lake entries (e.g., "DAM LAKE and connected Lily Lake and Dam Brook")
- Identifies cross-references (e.g., "See Whitefish Chain")
- Exports parsed data to both JSON and CSV formats

## Usage

### Running the Parser

1. Open the FishingRegs.TestConsole project
2. Run the console application
3. Select "Parse Special Regulations File" from the menu
4. The parser will:
   - Read the special-regulations.txt file
   - Parse all lake entries
   - Display sample entries and statistics
   - Export results to the `/data/parsed-output` folder

### Output Files

The parser creates the following files in `S:\src\rdl\BlazorFishingRegs\data\parsed-output\`:

1. **special-regulations-parsed.json**
   - Complete JSON representation of all parsed data
   - Includes nested species regulations
   - Formatted with indentation for readability

2. **special-regulations-lakes.csv**
   - One row per lake entry
   - Columns: LakeName, County, RegulationText, HasCrossReference, IsCompoundEntry, SpeciesCount

3. **special-regulations-species.csv**
   - One row per species regulation
   - Columns: LakeName, County, Species, RegulationType, DailyLimit, PossessionLimit, MinimumSize, MaximumSize, ProtectedSlot, IsCatchAndRelease, RegulationDetails

## Data Structures

### LakeRegulationEntry
- `LakeName`: Name of the lake/water body
- `County`: County where the lake is located
- `RegulationText`: Full text of the regulations
- `HasCrossReference`: Boolean indicating if this entry references another entry
- `IsCompoundEntry`: Boolean indicating if this is a compound entry (multiple connected water bodies)
- `SpeciesRegulations`: List of species-specific regulations

### SpeciesRegulation
- `Species`: Fish species name
- `RegulationType`: Type of regulation (DailyLimit, PossessionLimit, SizeLimit, ProtectedSlot, CatchAndRelease, Combined, General)
- `RegulationDetails`: Full text of the species regulation
- `DailyLimit`: Daily bag limit (if specified)
- `PossessionLimit`: Possession limit (if specified)
- `MinimumSize`: Minimum size restriction
- `MaximumSize`: Maximum size restriction
- `ProtectedSlot`: Protected slot size range
- `IsCatchAndRelease`: Boolean indicating catch-and-release only

## Parsing Logic

The parser uses the following approach:

1. **Section Extraction**: Locates the "Waters With Experimental and Special Regulations" section
2. **Text Cleaning**: Removes page headers, footers, and navigation elements
3. **Lake Entry Parsing**: Uses regex patterns to identify lake entries in format: `LAKE NAME (County) Regulation text`
4. **Species Parsing**: Extracts species-specific regulations by:
   - Identifying fish species mentions
   - Parsing limit numbers and size restrictions
   - Detecting catch-and-release keywords
   - Extracting protected slot ranges

## Examples

### Simple Entry
```
DEER LAKE near Effie (Itasca) Sunfish: daily limit 5.
```
Parsed as:
- Lake: "DEER LAKE near Effie"
- County: "Itasca"
- Species: Sunfish, DailyLimit: 5

### Complex Entry
```
BLUEBERRY LAKE (Wadena) Northern pike: all from 24-36" must be immediately released. Possession limit 3, only 1 over 36".
```
Parsed as:
- Lake: "BLUEBERRY LAKE"
- County: "Wadena"
- Species: Northern Pike
  - RegulationType: Combined
  - ProtectedSlot: "24-36""
  - PossessionLimit: 3

### Compound Entry
```
DAM LAKE and connected Lily Lake and Dam Brook (Aitkin) Sunfish: daily limit 10.
```
Parsed as:
- Lake: "DAM LAKE and connected Lily Lake and Dam Brook"
- County: "Aitkin"
- IsCompoundEntry: true
- Species: Sunfish, DailyLimit: 10

## Iterating on the Process

To refine the parsing:

1. Review the output files to identify parsing issues
2. Check the console output for statistics on parsed entries
3. Modify the regex patterns in `ParseLakeEntries()` or `ParseSpeciesRegulations()` as needed
4. Re-run the parser to generate updated output
5. Compare results to ensure improvements

## Next Steps

After validating the parsed data:

1. Use the JSON/CSV output to review data quality
2. Import the data into the database using the existing database population tools
3. Map the parsed data to the `FishingRegulation`, `WaterBody`, and `FishSpecies` models
4. Validate against the database schema requirements
