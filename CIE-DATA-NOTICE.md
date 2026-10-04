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

- CIE 2022, *CIE standard illuminant D50*,
  [DOI: 10.25039/CIE.DS.etgmuqt5](https://doi.org/10.25039/CIE.DS.etgmuqt5).
- CIE 2018, *Relative spectral power distributions of CIE illuminant D55*,
  [DOI: 10.25039/CIE.DS.qewfb3kp](https://doi.org/10.25039/CIE.DS.qewfb3kp).
- CIE 2018, *Relative spectral power distributions of CIE illuminant D75*,
  [DOI: 10.25039/CIE.DS.9fvcmrk4](https://doi.org/10.25039/CIE.DS.9fvcmrk4).
- CIE 2018, *Relative spectral power distributions of CIE illuminant C*,
  [DOI: 10.25039/CIE.DS.mjdd2enu](https://doi.org/10.25039/CIE.DS.mjdd2enu).
- CIE 2009, *CIE indoor daylight illuminant ID50*,
  [DOI: 10.25039/CIE.DS.r4gcnrzc](https://doi.org/10.25039/CIE.DS.r4gcnrzc).
- CIE 2009, *CIE indoor daylight illuminant ID65*,
  [DOI: 10.25039/CIE.DS.bd53qdqk](https://doi.org/10.25039/CIE.DS.bd53qdqk).
- CIE 2023, *CIE reference spectrum L41*,
  [DOI: 10.25039/CIE.DS.van56dfj](https://doi.org/10.25039/CIE.DS.van56dfj).
- CIE 2018, *Relative spectral power distributions of high pressure discharge lamp illuminants*,
  [DOI: 10.25039/CIE.DS.f6rvvnev](https://doi.org/10.25039/CIE.DS.f6rvvnev).
- CIE 2018, *Relative spectral power distributions of illuminants representing typical LED lamps, 1nm spacing*,
  [DOI: 10.25039/CIE.DS.dhcw57sd](https://doi.org/10.25039/CIE.DS.dhcw57sd).

Modifications in the generated numerical tables: the selected CSV columns are
represented as C# double literals, including every FL/HP/LED light column and
the illuminant catalog. The original 1 nm or 5 nm grid is preserved; undefined (`NaN`) CIE 1964 z-bar entries at 560–830 nm
are represented as zero for computation. Original CSV files are preserved without
these modifications. The fluorescent and LED 1 nm data are identified by CIE metadata as
`approximated`. The browser query uses linear interpolation when a requested
wavelength lies between original samples, and does not extrapolate. Its visible
wavelength background is a gamut-clipped, brightness-normalized sRGB display
approximation derived from the CIE 1931 observer; it does not modify the spectra. No CIE endorsement is implied.

For every archived dataset, the adjacent JSON preserves the official title,
creator, DOI, related publication, rights, data-quality and processing metadata,
together with the source URLs and downloaded CSV checksum. See `reference/README.md`
in the source repository for the inventory and update procedure.
