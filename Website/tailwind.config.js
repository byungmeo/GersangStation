/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{js,ts,jsx,tsx}"],
  theme: {
    extend: {
      keyframes: {
        wobble: {
          "0%, 100%": { transform: "rotate(-5deg)" },
          "50%": { transform: "rotate(5deg)" },
        },
        "show-up": {
          "0%": { transform: "translateY(100%)" },
          "90%": { transform: "translateY(100%)" },
          "100%": { transform: "translateY(0)" },
        },
        "show-from-right": {
          "0%": { transform: "translateX(100%)", opacity: 0 },
          "50%": { transform: "translateX(100%)", opacity: 0 },
          "100%": { transform: "translateX(0)", opacity: 1 },
        },
        "fade-in": {
          "0%": { opacity: 0 },
          "100%": { opacity: 1 },
        },
      },
    },
    animation: {
      wobble: "wobble 1s ease-in-out infinite",
      "show-up": "show-up 2s ease-in-out",
      "show-from-right": "show-from-right 1.4s ease-in-out",
      "fade-in-slow": "fade-in 1.8s ease-in-out",
    },
  },
  plugins: [],
};
