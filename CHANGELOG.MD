# Changelog

## v0.1.4
- [Fix] forward and backward slashes are now trimmed from the baseurl when the user is finished typing, adding a slash would cause the url to be valid while it was not.
- [Fix] null fields in json files are now handeld better, encountering a null field should in most cases no cause a crash
- [Fix] Incorrect deserialize functions in utility. The deserialize functions nolonger deserialize all endpoints but only a specified endpoint id or index.
- [Fix] Files in the content folder that do not match the structure of the cms documentID are now ignored by the utility script. A warning is thrown when an unusual file is found.

## v0.1.0
 -  First release