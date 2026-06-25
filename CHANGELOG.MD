# Changelog

## v0.2.1
- [Fix] Embedded content now downloads correctly. Previously references to embedded content like components would not update correctly.
## v0.2.0
- [Feature] Added support for Relation and Component fields (Nested content).
- [Fix] changed all added using to all unitywebrequest so garbage collection correctly cleans up the requests
- [Fix] Better handling of null fields in CMS class.
## v0.1.4
- [Fix] forward and backward slashes are now trimmed from the baseurl when the user is finished typing, adding a slash would cause the url to be valid while it was not.
- [Fix] null fields in json files are now handeld better, encountering a null field should in most cases not cause a crash
- [Fix] Incorrect deserialize functions in utility. The deserialize functions nolonger deserialize all endpoints but only a specified endpoint id or index.
- [Fix] Files in the content folder that do not match the structure of the cms documentID are now ignored by the utility script. A warning is thrown when an unusual file is found.

## v0.1.0
 -  First release
