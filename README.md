# APE

The APE DLL is a library of methods used by the APE GUI 
to apply a series of data transformations to data stored 
in a SQLite database. The set of transformations is called 
a Workflow, and is composed of sequential steps that define 
the data transformations to apply.

Workflow steps can include any valid SQL query, including:
* Table creation
* Table deletion
* Updating existing data
* Inserting data into a table

Each workflow step can optionally be associated with a workflow group.
When an application runs an APE workflow, it tells the APE DLL which
workflow groups should be used.

The DMS Analysis Manager uses the APE DLL library when processing
iTRAQ or TMT data to compute interference scores and run AScore.

## Contacts

Written by John Sandoval for the Department of Energy (PNNL, Richland, WA) \
E-mail: proteomics@pnnl.gov
Website: https://omics.pnl.gov/ or https://panomics.pnnl.gov/

## License

The APE library is licensed under the Apache License, Version 2.0; you may not use this 
file except in compliance with the License.  You may obtain a copy of the 
License at https://opensource.org/licenses/Apache-2.0
