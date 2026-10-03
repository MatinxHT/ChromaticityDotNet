# CIE data attribution

The library's authored software is licensed under MIT (see LICENSE).
The CIE reference datasets under `reference/` and the numerical tables transferred
into `Model/CieReferenceData.g.cs` are attributed to the International Commission
on Illumination (CIE), Vienna, AT, and licensed under
[Creative Commons Attribution-ShareAlike 4.0 International](https://creativecommons.org/licenses/by-sa/4.0/).
The package license expression lists both licenses because it includes these data.

Sources of the numerical data currently compiled into the library:

- CIE 2019, *Colour-matching functions of CIE 1931 standard colorimetric observer*,
  [DOI: 10.25039/CIE.DS.xvudnb9b](https://doi.org/10.25039/CIE.DS.xvudnb9b).
- CIE 2019, *CIE 1964 colour-matching functions, 10 degree observer*,
  [DOI: 10.25039/CIE.DS.sqksu2n5](https://doi.org/10.25039/CIE.DS.sqksu2n5).
- CIE 2018, *CIE standard illuminant A - 1 nm*,
  [DOI: 10.25039/CIE.DS.8jsxjrsn](https://doi.org/10.25039/CIE.DS.8jsxjrsn).
- CIE 2019, *CIE standard illuminant D65*,
  [DOI: 10.25039/CIE.DS.hjfjmt59](https://doi.org/10.25039/CIE.DS.hjfjmt59).
- CIE 2018, *Relative spectral power distributions of illuminants representing
  typical fluorescent lamps, 1nm wavelength steps*,
  [DOI: 10.25039/CIE.DS.54hy6srn](https://doi.org/10.25039/CIE.DS.54hy6srn).

Modifications in the generated numerical tables: the selected CSV columns are
represented as C# double literals; only FL2, FL7, FL11 and FL12 are compiled from
the fluorescent dataset; undefined (`NaN`) CIE 1964 z-bar entries at 560–830 nm
are represented as zero for computation. Original CSV files are preserved without
these modifications. The fluorescent 1 nm data are identified by CIE metadata as
`approximated`. No CIE endorsement is implied.

For every archived dataset, the adjacent JSON preserves the official title,
creator, DOI, related publication, rights, data-quality and processing metadata,
together with the source URLs and downloaded CSV checksum. See `reference/README.md`
in the source repository for the inventory and update procedure.
