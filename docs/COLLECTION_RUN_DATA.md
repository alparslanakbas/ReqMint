# Collection Runner data files

ReqMint can execute a saved collection once for each row in a local JSON or CSV file. Use normal `{{variableName}}` placeholders in request URLs, query parameters, headers, bodies, and content types. Values in the current data row override matching public or secret environment variables for that iteration; variables absent from the row continue to resolve from the selected environment.

## JSON

The root must be an array of flat objects. Values may be strings, numbers, or booleans.

```json
[
  {
    "orderId": "MINT-1001",
    "quantity": 2,
    "enabled": true
  },
  {
    "orderId": "MINT-1002",
    "quantity": 5,
    "enabled": false
  }
]
```

Objects, arrays, and `null` values inside a row are rejected so variable conversion remains explicit and predictable.

## CSV

The first record is the header. Every later record must contain the same number of fields. Standard quoted commas, doubled quotes, and quoted line endings are supported.

```csv
orderId,quantity,note
MINT-1001,2,"priority, same day"
MINT-1002,5,"customer said ""leave at reception"""
```

## Limits and privacy

- UTF-8 files only, up to 1 MiB.
- Up to 100 rows and 100 fields per row.
- Field names must match ReqMint template variable names and are compared without letter-case sensitivity.
- Values are limited to 4,096 characters.
- A run may execute at most 5,000 requests after multiplying collection requests by data rows.
- Iterations and requests execute sequentially.
- Data values are not persisted, added to request history, shown in results, or included in JSON/JUnit exports.

Treat data files as potentially sensitive local inputs. ReqMint does not upload them, but you should still avoid committing files containing credentials or personal data to Git.
