# Fishing Regulations Data

This folder contains the source fishing regulations PDF document and sample data for the Blazor AI application.

## Contents

- `fishing-regulations.pdf` - Source PDF document containing fishing regulations for all lakes
- `sample-lakes.json` - Sample lake data for testing
- `extracted-regulations/` - Extracted regulation data organized by lake

## PDF Structure Expected

The fishing regulations PDF should contain information organized by lake, including:
- Lake name and location
- Fishing seasons (dates)
- Species restrictions
- Bag limits
- Size limits
- Special regulations
- License requirements

## Data Processing Flow

1. Upload `fishing-regulations.pdf` to the application
2. AI extracts regulations for each lake
3. Data is structured and stored in the database
4. Users can select lakes to view specific regulations
