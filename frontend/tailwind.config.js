/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}"
  ],
  theme: {
    extend: {
      colors: {
        ncsInk: "#0b120d",
        ncsGreen: "#0b5d1e",
        ncsGreenDark: "#084616",
        ncsGold: "#d4a017",
        ncsLight: "#f4f7f3",
        ncsBorder: "#dfe7df"
      },
      fontFamily: {
        sans: ["Source Sans 3", "ui-sans-serif", "system-ui"],
        serif: ["Merriweather", "ui-serif", "serif"]
      },
      boxShadow: {
        card: "0 18px 35px rgba(11, 18, 13, 0.12)"
      }
    }
  },
  plugins: []
};
