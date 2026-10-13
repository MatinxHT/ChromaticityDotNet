"""Independent test fixtures: SciPy minimum search and Colour 0.4.7 CIE 1995 CRI.

Optional verification tool, not a library/build dependency. Requires numpy, scipy,
colour-science==0.4.7. Reads the archived CIE CSVs; does not download reference data.
Colour's CRI colourimetry/adaptation code supplies the expected R1–R14 values.
"""
import json
from pathlib import Path

import numpy as np
import colour
from scipy.optimize import minimize_scalar
from colour.quality.cri import tcs_colorimetry_data, colour_rendering_indexes

ROOT = Path(__file__).resolve().parents[1]


def table(name):
    return np.loadtxt(ROOT / "reference" / name, delimiter=",")


def xyz_uv(xyz):
    return colour.UCS_to_uv(colour.XYZ_to_UCS(xyz))


def planck(wavelengths, temperature):
    return colour.sd_blackbody(temperature, colour.SpectralShape(
        wavelengths[0], wavelengths[-1], 1)).values


def nearest(uv, wavelengths, cmfs):
    def objective(mired):
        point = xyz_uv(planck(wavelengths, 1e6 / mired) @ cmfs)
        return np.sum((point - uv) ** 2)
    result = minimize_scalar(objective, bounds=(40, 1000), method="bounded",
                             options={"xatol": 1e-10})
    return 1e6 / result.x


def fixtures():
    observers = [table("observers/CIE_xyz_1931_2deg.csv"),
                 np.nan_to_num(table("observers/CIE_xyz_1964_10deg.csv"))]
    cct = []
    for degree, observer in zip((2, 10), observers):
        for temperature in (1500, 2856, 5000, 6500, 12000, 24000):
            xyz = planck(observer[:, 0], temperature) @ observer[:, 1:]
            xy = colour.XYZ_to_xy(xyz)
            cct.append(dict(observer=degree, x=float(xy[0]), y=float(xy[1]),
                            cct=temperature, duv=0))
        uv = np.array([0.19783001, 0.31221333])  # D65 xy = (0.3127, 0.3290).
        xy = colour.UCS_uv_to_xy(uv)
        cct.append(dict(observer=degree, x=float(xy[0]), y=float(xy[1]),
                        cct=nearest(uv, observer[:, 0], observer[:, 1:])))

    shape = colour.SpectralShape(380, 780, 1)
    wavelengths = np.arange(380, 781)
    cmfs = colour.MultiSpectralDistributions(dict(zip(observers[0][:, 0], observers[0][:, 1:])),
        labels=["x_bar", "y_bar", "z_bar"]).trim(shape)
    # Match the library's documented linear resampling, using the official 14 TCS.
    tcs_table = table("test-samples/CIE_srf_cri.csv")
    tcs = {}
    for i, name in enumerate(colour.quality.SDS_TCS["CIE 1995"]):
        tcs[name] = colour.SpectralDistribution(dict(zip(tcs_table[:, 0], tcs_table[:, i + 1])),
            name=name, interpolator=colour.LinearInterpolator).align(shape)
    cri = []
    sources = [("D65", "CIE_std_illum_D65.csv", 1), ("A", "CIE_std_illum_A_1nm.csv", 1),
               ("FL2", "CIE_illum_FLs_1nm.csv", 2), ("FL7", "CIE_illum_FLs_1nm.csv", 7),
               ("FL11", "CIE_illum_FLs_1nm.csv", 11), ("LED-B1", "CIE_illum_LEDs_1nm.csv", 1),
               ("HP1", "CIE_illum_HPs.csv", 1)]
    for name, filename, column in sources:
        source = table("illuminants/" + filename)
        sd = colour.SpectralDistribution(dict(zip(source[:, 0], source[:, column])),
            interpolator=colour.LinearInterpolator).align(shape)
        uv = xyz_uv(sd.values @ cmfs.values)
        temperature = nearest(uv, wavelengths, cmfs.values)
        reference = colour.sd_blackbody(temperature, shape) if temperature < 5000 else \
            colour.sd_CIE_illuminant_D_series(colour.temperature.CCT_to_xy_CIE_D(temperature))
        reference.interpolator = colour.LinearInterpolator
        reference.align(shape)
        test_data = tcs_colorimetry_data(sd, reference, tcs, cmfs, chromatic_adaptation=True)
        reference_data = tcs_colorimetry_data(reference, reference, tcs, cmfs)
        indices = [float(item.Q_a) for item in colour_rendering_indexes(test_data, reference_data).values()]
        cri.append(dict(id=name, cct=temperature, ra=float(np.mean(indices[:8])), indices=indices))
    return dict(provenance="Colour 0.4.7 CIE 1995 CRI; SciPy bounded minimization; archived CIE CMFs/TCS/SPDs; 380–780 nm / 1 nm linear resampling; c2=0.014388 m K", cct=cct, cri=cri)


if __name__ == "__main__":
    result = fixtures()
    path = ROOT / "tests/ChromaticityDotNet.Tests/Fixtures/light-quality.json"
    path.parent.mkdir(exist_ok=True)
    path.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result["cri"], indent=2))
