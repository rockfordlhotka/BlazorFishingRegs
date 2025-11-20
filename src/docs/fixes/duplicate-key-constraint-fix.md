# Fix: Duplicate Key Constraint Violation in Database Population

## Problem

The application was experiencing a `DbUpdateException` with PostgreSQL error `23505` (duplicate key value violates unique constraint) during data ingestion:

```
23505: duplicate key value violates unique constraint "fishing_regulations_water_body_id_species_id_regulation_yea_key"
```

### Root Cause

The issue occurred in `RegulationDatabasePopulationService.PopulateDatabaseAsync` when:

1. **Batch Processing**: The method processes multiple lakes in a single batch and only calls `SaveChangesAsync` once at the end
2. **Name Variations**: Multiple lake entries in the AI extraction result can resolve to the same `WaterBody` due to:
   - Name variations (e.g., "Big Lake" and "BIG LAKE")
   - Duplicates in the source data
   - The `CleanLakeName` method normalizing different variations to the same name
3. **Same Species**: When the same species appears in multiple lake entries that map to the same water body, multiple `FishingRegulation` records with identical `(WaterBodyId, SpeciesId, RegulationYear)` keys get added to the EF Core change tracker
4. **Constraint Violation**: When `SaveChangesAsync` is finally called, the database constraint detects the duplicates and throws the exception

### Existing Protection (Insufficient)

The code already had duplicate detection logic that checked:
- ? Existing regulations in the database (via `GetByWaterBodyAndSpeciesAsync`)
- ? Regulations already in the current lake's `result.CreatedRegulations` list

However, it did **NOT** check:
- ? Regulations added by previous lake entries in the **same batch** that haven't been saved yet

## Solution

Added **batch-level duplicate tracking** using a `HashSet<(int WaterBodyId, int SpeciesId, int RegulationYear)>` to track all regulation keys added during the entire batch processing run.

### Changes Made

#### 1. Added Batch Tracking Field

```csharp
// Batch-level tracking to prevent duplicate regulations across the entire batch
private readonly HashSet<(int WaterBodyId, int SpeciesId, int RegulationYear)> _batchRegulationKeys = new();
```

#### 2. Clear Batch Tracking on Each Run

In `PopulateDatabaseAsync`:
```csharp
// Clear session caches for new processing run
_sessionSpeciesCache.Clear();
_batchRegulationKeys.Clear(); // NEW: Clear batch tracking

// ... process lakes ...

// Clean up cache
_sessionSpeciesCache.Clear();
_batchRegulationKeys.Clear(); // NEW: Clean up batch tracking
```

#### 3. Check Batch Tracking Before Adding Regulations

In `PopulateSingleLakeAsync`, before creating a new regulation:
```csharp
// Create the unique key for this regulation
var regulationKey = (result.WaterBody.Id, fishSpecies.Id, regulationYear);

// Check if we've already added this regulation in the current batch
if (_batchRegulationKeys.Contains(regulationKey))
{
    _logger.LogDebug($"Regulation for {fishSpecies.CommonName} in {result.WaterBody.Name} already exists in current batch, skipping");
    result.Warnings.Add($"Duplicate regulation detected for {fishSpecies.CommonName} in {result.WaterBody.Name} - using first occurrence");
    continue;
}
```

#### 4. Track Added Regulations

After successfully adding a regulation:
```csharp
await _unitOfWork.FishingRegulations.AddAsync(fishingRegulation, cancellationToken);
result.CreatedRegulations.Add(fishingRegulation);

// Track this regulation in our batch to prevent duplicates
_batchRegulationKeys.Add(regulationKey);
```

## Benefits

1. **Prevents Duplicate Key Errors**: Ensures no duplicate regulations are added to the database within a single batch
2. **Graceful Handling**: Logs warnings when duplicates are detected so users can investigate data quality issues
3. **Performance**: Uses efficient `HashSet` lookups (O(1)) instead of linear searches
4. **Data Quality**: First occurrence wins - ensures consistent behavior when duplicates exist in source data
5. **Memory Efficient**: Tracking only stores tuple keys, not full regulation objects

## Example Scenario

**Before the fix:**
```
Lake Entry 1: "BIG LAKE" ? Cleaned to "BIG LAKE" ? WaterBody ID = 123
  - Add regulation: (123, Walleye, 2024) ? Added to change tracker

Lake Entry 2: "Big Lake" ? Cleaned to "BIG LAKE" ? WaterBody ID = 123 (existing)
  - Add regulation: (123, Walleye, 2024) ? Added to change tracker (duplicate!)

SaveChangesAsync() ? ?? Duplicate key constraint violation
```

**After the fix:**
```
Lake Entry 1: "BIG LAKE" ? Cleaned to "BIG LAKE" ? WaterBody ID = 123
  - Add regulation: (123, Walleye, 2024) ? Added to change tracker
  - Track in _batchRegulationKeys: (123, Walleye, 2024)

Lake Entry 2: "Big Lake" ? Cleaned to "BIG LAKE" ? WaterBody ID = 123 (existing)
  - Check _batchRegulationKeys: (123, Walleye, 2024) exists
  - ?? Log warning and skip (don't add duplicate)

SaveChangesAsync() ? ? Success!
```

## Testing Recommendations

1. **Duplicate Lake Names**: Test with source data containing multiple variations of the same lake name
2. **Batch Processing**: Test with large batches (100+ lakes) to ensure tracking works across the entire batch
3. **Memory Profiling**: Verify that `_batchRegulationKeys` is properly cleared and doesn't cause memory leaks
4. **Warning Analysis**: Review processing warnings to identify data quality issues in source documents

## Related Files

- `FishingRegs.Services\Services\RegulationDatabasePopulationService.cs`
- `FishingRegs.Data\FishingRegsDbContext.cs` (constraint definition)

## Date

December 2024
