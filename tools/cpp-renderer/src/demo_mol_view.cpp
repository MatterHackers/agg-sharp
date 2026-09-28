// mol_view.cpp headless reproduction (default state: 400x400 bgr24 window, the first molecule of 1.sdf,
// thickness and label size 0.5, no rotation, scale 1, centred).
//
// Params (all optional): thickness label_size molecule angle_degrees scale center_x center_y.
//
// The molecules are read from agg-sharp's copy of 1.sdf (AGG_MOL_VIEW_SDF), the file the C# port embeds.
// The example's get_str trims a field's leading spaces only while the field ends before the line does, and
// counts its length down past zero on an all-space field (then reads an unset buffer); a field is read here
// as the columns it names, clipped to the line, trimmed and cut at the first space, as the port does. Every
// field 1.sdf holds reads the same both ways.
#include <cctype>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <string>
#include <vector>

#include "agg_basics.h"
#include "agg_math.h"
#include "agg_rendering_buffer.h"
#include "agg_rasterizer_scanline_aa.h"
#include "agg_scanline_p.h"
#include "agg_renderer_scanline.h"
#include "agg_conv_transform.h"
#include "agg_conv_stroke.h"
#include "agg_ellipse.h"
#include "agg_gsv_text.h"
#include "agg_pixfmt_rgb.h"
#include "ctrl/agg_slider_ctrl.h"

#include "common.h"

#ifndef AGG_MOL_VIEW_SDF
#define AGG_MOL_VIEW_SDF "1.sdf"
#endif

namespace {

enum atom_color_e {
    atom_color_general = 0,
    atom_color_N = 1,
    atom_color_O = 2,
    atom_color_S = 3,
    atom_color_P = 4,
    atom_color_halogen = 5,
    end_of_atom_colors
};

struct atom_type {
    double x, y;
    std::string label;
    int charge;
    unsigned color_idx;
};

struct bond_type {
    unsigned idx1, idx2;
    double x1, y1, x2, y2;
    unsigned order;
    int stereo;
    int topology;
};

struct molecule {
    std::vector<atom_type> atoms;
    std::vector<bond_type> bonds;
    std::string name;
    double avr_len = 0;
};

// Columns pos..pos+len-1 (1-based) of the line, clipped to it, leading spaces skipped, cut at the next space.
std::string get_str(const std::string& buf, int pos, int len) {
    size_t start = size_t(pos - 1);
    if (start >= buf.size()) return std::string();
    std::string f = buf.substr(start, size_t(len));
    size_t i = 0;
    while (i < f.size() && isspace((unsigned char)f[i])) i++;
    size_t j = i;
    while (j < f.size() && !isspace((unsigned char)f[j])) j++;
    return f.substr(i, j - i);
}

int get_int(const std::string& buf, int pos, int len) { return atoi(get_str(buf, pos, len).c_str()); }
double get_dbl(const std::string& buf, int pos, int len) { return atof(get_str(buf, pos, len).c_str()); }

// fgets(buf, 510) then trim_cr_lf: leading and trailing CR/LF go.
bool read_line(FILE* fd, std::string& line) {
    char buf[512];
    if (!fgets(buf, 510, fd)) return false;
    line = buf;
    size_t b = 0;
    while (b < line.size() && (line[b] == '\n' || line[b] == '\r')) b++;
    size_t e = line.size();
    while (e > b && (line[e - 1] == '\n' || line[e - 1] == '\r')) e--;
    line = line.substr(b, e - b);
    return true;
}

bool read_molecule(FILE* fd, molecule& m) {
    std::string buf;
    if (!read_line(fd, buf)) return false;
    m.name = buf.substr(0, 128);
    if (!read_line(fd, buf)) return false;
    if (!read_line(fd, buf)) return false;
    if (!read_line(fd, buf)) return false;
    unsigned num_atoms = get_int(buf, 1, 3);
    unsigned num_bonds = get_int(buf, 4, 3);
    if (num_atoms == 0 || num_bonds == 0) return false;
    m.atoms.assign(num_atoms, atom_type());
    m.bonds.assign(num_bonds, bond_type());

    for (unsigned i = 0; i < num_atoms; i++) {
        if (!read_line(fd, buf)) return false;
        atom_type& a = m.atoms[i];
        a.x = get_dbl(buf, 1, 10);
        a.y = get_dbl(buf, 11, 10);
        a.label = get_str(buf, 32, 3);
        a.charge = get_int(buf, 39, 1);
        if (a.charge) a.charge = 4 - a.charge;
        a.color_idx = atom_color_general;
        if (a.label == "N") a.color_idx = atom_color_N;
        if (a.label == "O") a.color_idx = atom_color_O;
        if (a.label == "S") a.color_idx = atom_color_S;
        if (a.label == "P") a.color_idx = atom_color_P;
        if (a.label == "F" || a.label == "Cl" || a.label == "Br" || a.label == "I") a.color_idx = atom_color_halogen;
    }

    m.avr_len = 0.0;
    for (unsigned i = 0; i < num_bonds; i++) {
        if (!read_line(fd, buf)) return false;
        bond_type& b = m.bonds[i];
        b.idx1 = get_int(buf, 1, 3) - 1;
        b.idx2 = get_int(buf, 4, 3) - 1;
        if (b.idx1 >= num_atoms || b.idx2 >= num_atoms) return false;
        b.x1 = m.atoms[b.idx1].x;
        b.y1 = m.atoms[b.idx1].y;
        b.x2 = m.atoms[b.idx2].x;
        b.y2 = m.atoms[b.idx2].y;
        b.order = get_int(buf, 7, 3);
        b.stereo = get_int(buf, 10, 3);
        b.topology = get_int(buf, 13, 3);
        m.avr_len += sqrt((b.x1 - b.x2) * (b.x1 - b.x2) + (b.y1 - b.y2) * (b.y1 - b.y2));
    }
    m.avr_len /= double(num_bonds);

    while (read_line(fd, buf)) {
        if (!buf.empty() && buf[0] == '$') return true;
    }
    return false;
}

// agg::line, agg::solid_wedge and agg::dashed_wedge from the example, as they are.
class line {
public:
    void init(double x1, double y1, double x2, double y2) { m_x1 = x1; m_y1 = y1; m_x2 = x2; m_y2 = y2; }
    void thickness(double th) { m_thickness = th; }
    void rewind(unsigned) {
        agg::calc_orthogonal(m_thickness * 0.5, m_x1, m_y1, m_x2, m_y2, &m_dx, &m_dy);
        m_vertex = 0;
    }
    unsigned vertex(double* x, double* y) {
        switch (m_vertex) {
        case 0: *x = m_x1 - m_dx; *y = m_y1 - m_dy; m_vertex++; return agg::path_cmd_move_to;
        case 1: *x = m_x2 - m_dx; *y = m_y2 - m_dy; m_vertex++; return agg::path_cmd_line_to;
        case 2: *x = m_x2 + m_dx; *y = m_y2 + m_dy; m_vertex++; return agg::path_cmd_line_to;
        case 3: *x = m_x1 + m_dx; *y = m_y1 + m_dy; m_vertex++; return agg::path_cmd_line_to;
        }
        return agg::path_cmd_stop;
    }

private:
    double m_x1 = 0.0, m_y1 = 0.0, m_x2 = 1.0, m_y2 = 0.0, m_dx = 0, m_dy = 0, m_thickness = 0.1;
    unsigned m_vertex = 0;
};

class solid_wedge {
public:
    void init(double x1, double y1, double x2, double y2) { m_x1 = x1; m_y1 = y1; m_x2 = x2; m_y2 = y2; }
    void thickness(double th) { m_thickness = th; }
    void rewind(unsigned) {
        agg::calc_orthogonal(m_thickness * 2.0, m_x1, m_y1, m_x2, m_y2, &m_dx, &m_dy);
        m_vertex = 0;
    }
    unsigned vertex(double* x, double* y) {
        switch (m_vertex) {
        case 0: *x = m_x1; *y = m_y1; m_vertex++; return agg::path_cmd_move_to;
        case 1: *x = m_x2 - m_dx; *y = m_y2 - m_dy; m_vertex++; return agg::path_cmd_line_to;
        case 2: *x = m_x2 + m_dx; *y = m_y2 + m_dy; m_vertex++; return agg::path_cmd_line_to;
        }
        return agg::path_cmd_stop;
    }

private:
    double m_x1 = 0.0, m_y1 = 0.0, m_x2 = 1.0, m_y2 = 0.0, m_dx = 0, m_dy = 0, m_thickness = 0.1;
    unsigned m_vertex = 0;
};

class dashed_wedge {
public:
    // The example's init swaps the ends: the dashes widen from the second atom towards the first.
    void init(double x1, double y1, double x2, double y2) { m_x1 = x2; m_y1 = y2; m_x2 = x1; m_y2 = y1; }
    void thickness(double th) { m_thickness = th; }
    void rewind(unsigned) {
        double dx, dy;
        agg::calc_orthogonal(m_thickness * 2.0, m_x1, m_y1, m_x2, m_y2, &dx, &dy);
        m_xt2 = m_x2 - dx;
        m_yt2 = m_y2 - dy;
        m_xt3 = m_x2 + dx;
        m_yt3 = m_y2 + dy;
        m_vertex = 0;
    }
    unsigned vertex(double* x, double* y) {
        if (m_vertex < m_num_dashes * 4) {
            if ((m_vertex % 4) == 0) {
                double k1 = double(m_vertex / 4) / double(m_num_dashes);
                double k2 = k1 + 0.4 / double(m_num_dashes);
                m_xd[0] = m_x1 + (m_xt2 - m_x1) * k1;
                m_yd[0] = m_y1 + (m_yt2 - m_y1) * k1;
                m_xd[1] = m_x1 + (m_xt2 - m_x1) * k2;
                m_yd[1] = m_y1 + (m_yt2 - m_y1) * k2;
                m_xd[2] = m_x1 + (m_xt3 - m_x1) * k2;
                m_yd[2] = m_y1 + (m_yt3 - m_y1) * k2;
                m_xd[3] = m_x1 + (m_xt3 - m_x1) * k1;
                m_yd[3] = m_y1 + (m_yt3 - m_y1) * k1;
                *x = m_xd[0];
                *y = m_yd[0];
                m_vertex++;
                return agg::path_cmd_move_to;
            }
            *x = m_xd[m_vertex % 4];
            *y = m_yd[m_vertex % 4];
            m_vertex++;
            return agg::path_cmd_line_to;
        }
        return agg::path_cmd_stop;
    }

private:
    double m_x1 = 0.0, m_y1 = 0.0, m_x2 = 1.0, m_y2 = 0.0;
    double m_xt2 = 0, m_yt2 = 0, m_xt3 = 0, m_yt3 = 0;
    double m_xd[4] = {0, 0, 0, 0}, m_yd[4] = {0, 0, 0, 0};
    double m_thickness = 0.1;
    unsigned m_num_dashes = 8;
    unsigned m_vertex = 0;
};

class bond_vertex_generator {
    enum bond_style_e { bond_single, bond_wedged_solid, bond_wedged_dashed, bond_double, bond_double_left, bond_double_right, bond_triple };

public:
    bond_vertex_generator(const bond_type& bond, double thickness) : m_bond(bond), m_thickness(thickness), m_style(bond_single) {
        if (bond.order == 1) {
            if (bond.stereo == 1) m_style = bond_wedged_solid;
            if (bond.stereo == 6) m_style = bond_wedged_dashed;
        }
        if (bond.order == 2) {
            m_style = bond_double;
            if (bond.topology == 1) m_style = bond_double_left;
            if (bond.topology == 2) m_style = bond_double_right;
        }
        if (bond.order == 3) m_style = bond_triple;
        m_line1.thickness(thickness);
        m_line2.thickness(thickness);
        m_solid_wedge.thickness(thickness);
        m_dashed_wedge.thickness(thickness);
    }

    void rewind(unsigned) {
        double dx, dy;
        switch (m_style) {
        case bond_wedged_solid:
            m_solid_wedge.init(m_bond.x1, m_bond.y1, m_bond.x2, m_bond.y2);
            m_solid_wedge.rewind(0);
            break;
        case bond_wedged_dashed:
            m_dashed_wedge.init(m_bond.x1, m_bond.y1, m_bond.x2, m_bond.y2);
            m_dashed_wedge.rewind(0);
            break;
        case bond_double:
        case bond_double_left:
        case bond_double_right:
            // Every double bond is drawn as two lines either side of the bond (the example's left/right
            // offsets are commented out, pending ring perception).
            agg::calc_orthogonal(m_thickness, m_bond.x1, m_bond.y1, m_bond.x2, m_bond.y2, &dx, &dy);
            m_line1.init(m_bond.x1 - dx, m_bond.y1 - dy, m_bond.x2 - dx, m_bond.y2 - dy);
            m_line1.rewind(0);
            m_line2.init(m_bond.x1 + dx, m_bond.y1 + dy, m_bond.x2 + dx, m_bond.y2 + dy);
            m_line2.rewind(0);
            m_status = 0;
            break;
        default:
            // A triple bond is drawn as a single one, as in the example.
            m_line1.init(m_bond.x1, m_bond.y1, m_bond.x2, m_bond.y2);
            m_line1.rewind(0);
            break;
        }
    }

    unsigned vertex(double* x, double* y) {
        unsigned flag = agg::path_cmd_stop;
        switch (m_style) {
        case bond_wedged_solid: return m_solid_wedge.vertex(x, y);
        case bond_wedged_dashed: return m_dashed_wedge.vertex(x, y);
        case bond_double_left:
        case bond_double_right:
        case bond_double:
            if (m_status == 0) {
                flag = m_line1.vertex(x, y);
                if (flag == agg::path_cmd_stop) m_status = 1;
            }
            if (m_status == 1) flag = m_line2.vertex(x, y);
            return flag;
        default:
            break;
        }
        return m_line1.vertex(x, y);
    }

private:
    const bond_type& m_bond;
    double m_thickness;
    bond_style_e m_style;
    line m_line1;
    line m_line2;
    solid_wedge m_solid_wedge;
    dashed_wedge m_dashed_wedge;
    unsigned m_status = 0;
};

} // namespace

int render_mol_view(unsigned w, unsigned h, const std::vector<double>& params, const char* out) {
    typedef agg::pixfmt_bgr24 pixfmt;
    typedef agg::renderer_base<pixfmt> renderer_base;
    typedef agg::renderer_scanline_aa_solid<renderer_base> renderer_solid;
    const double start_width = 400, start_height = 400;

    std::vector<molecule> molecules;
    FILE* fd = fopen(AGG_MOL_VIEW_SDF, "r");
    if (!fd) {
        fprintf(stderr, "Failed to open '%s'\n", AGG_MOL_VIEW_SDF);
        return 1;
    }
    for (unsigned i = 0; i < 100; i++) {
        molecule m;
        if (!read_molecule(fd, m)) break;
        molecules.push_back(m);
    }
    fclose(fd);

    agg::slider_ctrl<agg::rgba8> m_thickness(5, 5, start_width - 5, 12);
    agg::slider_ctrl<agg::rgba8> m_text_size(5, 20, start_width - 5, 27);
    m_thickness.label("Thickness=%3.2f");
    m_text_size.label("Label Size=%3.2f");
    if (params.size() > 0) m_thickness.value(params[0]);
    if (params.size() > 1) m_text_size.value(params[1]);
    unsigned m_cur_molecule = params.size() > 2 ? unsigned(params[2]) : 0;
    double m_angle = params.size() > 3 ? agg::deg2rad(params[3]) : 0.0;
    double m_scale = params.size() > 4 ? params[4] : 1.0;
    double m_center_x = params.size() > 5 ? params[5] : start_width / 2;
    double m_center_y = params.size() > 6 ? params[6] : start_height / 2;
    if (m_cur_molecule >= molecules.size()) {
        fprintf(stderr, "mol_view: molecule %u of %u\n", m_cur_molecule, unsigned(molecules.size()));
        return 1;
    }

    agg::srgba8 m_atom_colors[end_of_atom_colors];
    m_atom_colors[atom_color_general] = agg::srgba8(0, 0, 0);
    m_atom_colors[atom_color_N] = agg::srgba8(0, 0, 120);
    m_atom_colors[atom_color_O] = agg::srgba8(200, 0, 0);
    m_atom_colors[atom_color_S] = agg::srgba8(120, 120, 0);
    m_atom_colors[atom_color_P] = agg::srgba8(80, 50, 0);
    m_atom_colors[atom_color_halogen] = agg::srgba8(0, 200, 0);

    // on_draw(); trans_affine_resizing() is the identity at the window's own size.
    double width = w;
    double height = h;

    headless::canvas cv(w, h, 3);
    agg::rasterizer_scanline_aa<> ras;
    agg::scanline_p8 sl;
    pixfmt pixf(cv.rbuf);
    renderer_base rb(pixf);
    renderer_solid rs(rb);

    ras.clip_box(0, 0, rb.width(), rb.height());
    rb.clear(agg::rgba(1, 1, 1));

    const molecule& mol = molecules[m_cur_molecule];
    double min_x = 1e100, max_x = -1e100, min_y = 1e100, max_y = -1e100;
    for (const atom_type& a : mol.atoms) {
        if (a.x < min_x) min_x = a.x;
        if (a.y < min_y) min_y = a.y;
        if (a.x > max_x) max_x = a.x;
        if (a.y > max_y) max_y = a.y;
    }

    agg::trans_affine mtx;
    mtx *= agg::trans_affine_translation(-(max_x + min_x) * 0.5, -(max_y + min_y) * 0.5);

    double scale = width / (max_x - min_x);
    double t = height / (max_y - min_y);
    if (scale > t) scale = t;

    double text_size = mol.avr_len * m_text_size.value() / 4.0;
    double thickness = mol.avr_len / sqrt(m_scale < 0.0001 ? 0.0001 : m_scale) / 8.0;

    mtx *= agg::trans_affine_scaling(scale * 0.80, scale * 0.80);
    mtx *= agg::trans_affine_rotation(m_angle);
    mtx *= agg::trans_affine_scaling(m_scale, m_scale);
    mtx *= agg::trans_affine_translation(m_center_x, m_center_y);

    rs.color(agg::rgba(0, 0, 0));
    for (const bond_type& b : mol.bonds) {
        bond_vertex_generator bond(b, m_thickness.value() * thickness);
        agg::conv_transform<bond_vertex_generator> tr(bond, mtx);
        ras.add_path(tr);
        agg::render_scanlines(ras, sl, rs);
    }

    agg::ellipse ell;
    agg::conv_transform<agg::ellipse> tr(ell, mtx);
    for (const atom_type& a : mol.atoms) {
        if (a.label != "C") {
            ell.init(a.x, a.y, text_size * 2.5, text_size * 2.5, 20);
            ras.add_path(tr);
            rs.color(agg::rgba(1.0, 1.0, 1.0));
            agg::render_scanlines(ras, sl, rs);
        }
    }

    text_size *= 3.0;

    agg::gsv_text label;
    agg::conv_stroke<agg::gsv_text> ls(label);
    agg::conv_transform<agg::conv_stroke<agg::gsv_text> > lo(ls, mtx);
    ls.line_join(agg::round_join);
    ls.line_cap(agg::round_cap);
    ls.approximation_scale(mtx.scale());
    for (const atom_type& a : mol.atoms) {
        if (a.label != "C") {
            ls.width(m_thickness.value() * thickness);
            label.text(a.label.c_str());
            label.start_point(a.x - text_size / 2, a.y - text_size / 2);
            label.size(text_size);
            ras.add_path(lo);
            rs.color(m_atom_colors[a.color_idx]);
            agg::render_scanlines(ras, sl, rs);
        }
    }

    ls.approximation_scale(1.0);
    ls.width(1.5);
    label.text(mol.name.c_str());
    label.size(10.0);
    label.start_point(10.0, start_height - 20.0);
    ras.reset();
    ras.add_path(ls);
    rs.color(agg::rgba(0, 0, 0));
    agg::render_scanlines(ras, sl, rs);

    agg::render_ctrl(ras, sl, rb, m_thickness);
    agg::render_ctrl(ras, sl, rb, m_text_size);

    return headless::write_raw(out, pixf, w, h) ? 0 : 1;
}
