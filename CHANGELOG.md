# EBICS.Net ChangeLog

## Version 0.1.1

* Added InstrId field for CCT command required by some banks
* Added BinaryData field for STA response as it may contain binary data (such as zip container with response files)
* Added support for multiple flavors of STA command derivatives. 
Some of those are listed on 2020-07-13-EBICS_Annex_BTF-External_Codes.zip on (EBICS website)[ebics.org], however this list is not complete. This change is rather temporary hack that gets job done, however this is to be changed/refined in future by having separe classes depending on behavior.
* KeyUtils shortcut for certificate generation and X509Store certificate storage shortcuts
* Namespace changed to EbicsNet, Readme updated, ChangeLog added

## Version 0.1.0

* Forked from https://github.com/hohlerde/NetEbics

