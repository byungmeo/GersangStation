interface TagProps {
  text: string;
}

function Tag({ text }: TagProps) {
  return <div className="bg-white rounded-full px-4 py-[6px] text-[#6151fc] font-semibold">{text}</div>;
}

export default Tag;
